use futures_util::TryStreamExt;
use http::Uri;
use orchard::{keys::Scope, tree::MerkleHashOrchard};
use prost::bytes::Buf;
use rusqlite::{named_params, Connection};
use std::{borrow::Borrow, collections::HashMap, ops::Range, path::Path, sync::Arc};
use tokio::{
    select,
    sync::{mpsc, Mutex},
};
use tokio_util::sync::CancellationToken;
use tonic::{transport::Channel, Status};
use tracing::{debug, info};
use uniffi::deps::anyhow;
use zcash_client_sqlite::{error::SqliteClientError, AccountId, WalletDb};
use zcash_keys::address::UnifiedAddress;
use zcash_primitives::{
    consensus::{BlockHeight, BranchId, Network, NetworkUpgrade, Parameters},
    legacy::TransparentAddress,
    memo::Memo,
    merkle_tree::HashSer,
    transaction::{
        components::{Amount, OutPoint},
        Transaction, TxId,
    },
};

use zcash_client_backend::{
    data_api::{
        chain::{scan_cached_blocks, ChainState, CommitmentTreeRoot},
        scanning::{ScanPriority, ScanRange},
        wallet::decrypt_and_store_transaction,
        TransparentAddressSyncInfo, WalletCommitmentTrees, WalletRead, WalletWrite,
    },
    encoding::AddressCodec,
    proto::{
        compact_formats::CompactBlock,
        service::{
            self, compact_tx_streamer_client::CompactTxStreamerClient, BlockId, BlockRange, Empty,
            TransparentAddressBlockFilter, TxFilter,
        },
    },
    wallet::WalletTransparentOutput,
};

use crate::{
    backing_store::Db,
    block_source::BlockCacheError,
    error::Error,
    grpc::get_client,
    interop::{SyncUpdate, SyncUpdateData, TransactionNote},
    lightclient::parse_network,
    resilience::webrequest_with_retry,
    sql_statements::GET_TRANSACTIONS_SQL,
};

type ChainError =
    zcash_client_backend::data_api::chain::error::Error<SqliteClientError, BlockCacheError>;

const TADDR_INDEX_GAP_LIMIT: u32 = 20;

/// The number of sapling spends+outputs and orchard actions that should be in memory at any time.
const BLOCK_ACTIONS_MEMORY_LIMIT: usize = 500_000;

/// The capacity of the channel that receives vectors of CompactBlock.
///
/// This should be a relatively low number for scanning efficiency, but
/// high enough that we don't wait too long for download before starting to scan.
const CHUNK_CHANNEL_CAPACITY: usize = 10;

/// The approximate number of actions for each chunk that we submit to the downloaded channel.
/// We want this to contain at least (num_threads - 1) * 100 + 1 outputs in order to maximize throughput
/// during trial decryption and other stages.
/// https://discord.com/channels/809218587167293450/1250828701864693761/1250942856198230086
const BLOCKS_CHUNK_THRESHOLD: usize = BLOCK_ACTIONS_MEMORY_LIMIT / CHUNK_CHANNEL_CAPACITY;

pub async fn sync<P: AsRef<Path>>(
    uri: Uri,
    data_file: P,
    progress: Option<Box<dyn SyncUpdate>>,
    min_confirmations: u32,
    continually: bool,
    cancellation_token: CancellationToken,
) -> Result<SyncUpdateData, Error> {
    let mut client = get_client(uri.clone()).await?;
    let info = webrequest_with_retry(
        || async {
            Ok(client
                .clone()
                .get_lightd_info(service::Empty {})
                .await?
                .into_inner())
        },
        cancellation_token.clone(),
    )
    .await?;
    let state = SyncState {
        cancellation_token,
        min_confirmations,
        network: parse_network(&info)?,
        progress: Arc::new(progress),
    };

    let mut db = Db::load(&data_file, state.network)?;
    let conn = Connection::open(&data_file)?;

    // 1) Download note commitment tree data from lightwalletd
    // 2) Pass the commitment tree data to the database.
    update_subtree_roots(&mut client.clone(), &mut db.data).await?;

    let mut status = SyncUpdateData {
        current_step: 0,
        total_steps: 0,
        last_fully_scanned_block: None,
        tip_height: 0,
        last_error: None,
    };

    loop {
        // 3) Download chain tip metadata from lightwalletd
        status.tip_height = webrequest_with_retry(
            || async {
                Ok(client
                    .clone()
                    .get_latest_block(service::ChainSpec::default())
                    .await?
                    .get_ref()
                    .height)
            },
            state.cancellation_token.clone(),
        )
        .await?
        .try_into()
        .map_err(|e| Error::Internal(format!("Invalid block height: {}", e)))?;

        // 4) Notify the wallet of the updated chain tip.
        db.data.update_chain_tip(status.tip_height.into())?;

        fn report_new_transactions<P: AsRef<Path>>(
            txids: Vec<TxId>,
            progress: &Option<Box<dyn SyncUpdate>>,
            data_file: &P,
            db: &mut Db,
            conn: &Connection,
            network: Network,
        ) -> Result<(), Error> {
            if !txids.is_empty() {
                initialize_transaction_fees(db, conn)?;
                if let Some(sink) = progress.as_ref() {
                    let mut conn = Connection::open(data_file)?;
                    let new_transactions =
                        get_transactions(db, &mut conn, &network, None, None, None)?
                            .iter()
                            .filter(|r| {
                                TryInto::<[u8; 32]>::try_into(r.txid.clone())
                                    .map(|a| txids.contains(&TxId::from_bytes(a)))
                                    .unwrap_or(false)
                            })
                            .cloned()
                            .collect::<Vec<_>>();
                    if !new_transactions.is_empty() {
                        sink.report_transactions(new_transactions);
                    }
                }
            }

            Ok(())
        }

        fn report_transactions_in_range<P: AsRef<Path>>(
            range: &Range<BlockHeight>,
            progress: &Option<Box<dyn SyncUpdate>>,
            data_file: &P,
            db: &mut Db,
            conn: &Connection,
            network: &Network,
        ) -> Result<(), Error> {
            initialize_transaction_fees(db, conn)?;
            if let Some(sink) = progress.as_ref() {
                let mut conn = Connection::open(data_file)?;
                let new_transactions = get_transactions(
                    db,
                    &mut conn,
                    network,
                    None,
                    Some(range.start.into()),
                    Some(range.end.into()),
                )?
                .to_vec();
                if !new_transactions.is_empty() {
                    sink.report_transactions(new_transactions);
                }
            }

            Ok(())
        }

        // Download all the transparent ops related to the wallet first.
        // We don't need batches for this as that would just multiply the number of LWD requests we have to make.
        let mut taddrs = db.data.get_transparent_addresses_and_sync_heights()?;
        let mut taddrs_to_scan = taddrs.clone();
        while !taddrs_to_scan.is_empty() {
            for addr_info in taddrs.iter_mut().filter(|a| taddrs_to_scan.contains(a)) {
                let txids = download_transparent_transactions(
                    &mut client,
                    &mut db,
                    &state.network,
                    &addr_info.address,
                    addr_info.height,
                    status.tip_height.into(),
                    state.cancellation_token.clone(),
                )
                .await?;
                if !txids.is_empty() {
                    addr_info.used = true;
                    report_new_transactions(
                        txids,
                        state.progress.borrow(),
                        &data_file,
                        &mut db,
                        &conn,
                        state.network,
                    )?;
                }
            }

            taddrs_to_scan = fill_in_taddrs_to_gap_limit(&mut taddrs, &mut db.data)?;
        }

        // 5) Get the suggested scan ranges from the wallet database
        let mut scan_ranges = db.data.suggest_scan_ranges()?;

        // 6) Run the following loop until the wallet's view of the chain tip as of the previous wallet
        //    session is valid.
        loop {
            // If there is a range of blocks that needs to be verified, it will always be returned as
            // the first element of the vector of suggested ranges.
            match scan_ranges.first() {
                Some(scan_range) if scan_range.priority() == ScanPriority::Verify => {
                    // Download and scan the blocks and check for scanning errors that indicate that the wallet's chain tip
                    // is out of sync with blockchain history.
                    let scan_result = download_and_scan_blocks(
                        &mut client,
                        Db::load(&data_file, state.network)?,
                        &state,
                        None,
                        scan_range,
                    )
                    .await?;

                    if scan_result.priorities_changed {
                        // The suggested scan ranges have been updated, so we re-request.
                        scan_ranges = db.data.suggest_scan_ranges()?;
                    } else {
                        // At this point, the cache and scanned data are locally
                        // consistent (though not necessarily consistent with the
                        // latest chain tip - this would be discovered the next time
                        // this codepath is executed after new blocks are received) so
                        // we can break out of the loop.
                        break;
                    }
                }
                _ => {
                    // Nothing to verify; break out of the loop
                    break;
                }
            }
        }

        // 7) Loop over the remaining suggested scan ranges, retrieving the requested data and calling
        //    `scan_cached_blocks` on each range. Periodically, or if a continuity error is
        //    encountered, this process should be repeated starting at step (3).
        // Download the blocks in `scan_range` into the block source. While in this example this
        // step is performed in-line, it's fine for the download of scan ranges to be asynchronous
        // and for the scanner to process the downloaded ranges as they become available in a
        // separate thread. The scan ranges should also be broken down into smaller chunks as
        // appropriate, and for ranges with priority `Historic` it can be useful to download and
        // scan the range in reverse order (to discover more recent unspent notes sooner), or from
        // the start and end of the range inwards.
        let scan_ranges = db.data.suggest_scan_ranges()?;
        debug!("Suggested ranges: {:?}", scan_ranges);

        // The total_steps is the sum of the current_step and the sum of the lengths of the scan_ranges.
        status.total_steps =
            status.current_step + scan_ranges.iter().map(|r| r.len()).sum::<usize>() as u64;

        update_and_report_status(&mut status, &db.data, min_confirmations, &state.progress)?;

        let mut caught_up = true;
        for scan_range in scan_ranges.into_iter() {
            let scan_result = download_and_scan_blocks(
                &mut client,
                Db::load(&data_file, state.network)?,
                &state,
                Some(&status),
                &scan_range,
            )
            .await?;
            if let Some(s) = scan_result.status {
                status = s;
            }

            // Download and decrypt the full transactions we found in the compact blocks
            // so we can save their memos to the database.
            download_full_shielded_transactions(
                &mut client,
                &data_file,
                &mut db,
                &state.network,
                state.cancellation_token.clone(),
            )
            .await?;

            // Report all transactions that are in the block range we just scanned,
            // even if we didn't just download them (which would have only included shielded transactions).
            // Transparent transactions in this range only just now got assigned their block height,
            // so reporting them (again) at this point is good for the client.
            report_transactions_in_range(
                scan_range.block_range(),
                &state.progress,
                &data_file,
                &mut db,
                &conn,
                &state.network,
            )?;

            update_and_report_status(&mut status, &db.data, min_confirmations, &state.progress)?;

            if scan_result.priorities_changed {
                // The suggested scan ranges have been updated (either due to a continuity
                // error or because a higher priority range has been added).
                caught_up = false;
                break;
            }
        }

        if caught_up {
            update_status(&mut status, &db.data, min_confirmations)?;

            if !continually {
                return Ok(status);
            }

            report_status(&status, &state.progress);

            // We'll loop around again when the next block is mined.
            // Eventually we should actually do something with the transactions in the mempool too.
            // WARNING: This is vulnerable to a race condition, because if a new block has *already* been mined
            // but not noticed above, we'll end up waiting for yet *another* block to be mined.
            select! {
                _ = state.cancellation_token.cancelled() => Err(Status::cancelled("Request cancelled")),
                _ = watch_mempool(&mut client) => Ok(()),
            }?;
        }
    }
}

fn update_status<'a>(
    status: &'a mut SyncUpdateData,
    data: &WalletDb<Connection, Network>,
    min_confirmations: u32,
) -> Result<&'a SyncUpdateData, Error> {
    status.last_fully_scanned_block = data.block_fully_scanned()?.map(|b| b.block_height().into());

    // Disabled for now because it's unstable -- it goes backwards, jumps around, etc.
    if false {
        if let Some(wallet_progress) = data
            .get_wallet_summary(min_confirmations)
            .unwrap_or(None)
            .and_then(|s| s.scan_progress())
        {
            status.current_step = *wallet_progress.numerator();
            status.total_steps = *wallet_progress.denominator();
        }
    }

    Ok(status)
}

fn report_status(status: &SyncUpdateData, progress: &Option<Box<dyn SyncUpdate>>) {
    if let Some(sink) = progress.as_ref() {
        sink.update_status(status.clone());
    }
}

fn update_and_report_status(
    status: &mut SyncUpdateData,
    data: &WalletDb<Connection, Network>,
    min_confirmations: u32,
    progress: &Option<Box<dyn SyncUpdate>>,
) -> Result<(), Error> {
    report_status(update_status(status, data, min_confirmations)?, progress);
    Ok(())
}

struct DownloadAndScanResult {
    priorities_changed: bool,
    status: Option<SyncUpdateData>,
}

fn fill_in_taddrs_to_gap_limit(
    taddrs: &mut Vec<TransparentAddressSyncInfo<AccountId>>,
    db: &mut WalletDb<Connection, Network>,
) -> Result<Vec<TransparentAddressSyncInfo<AccountId>>, Error> {
    // Transform the vector of addresses into this structure:
    // Account -> (DiversifierIndex -> TransparentAddressSyncInfo)
    let mut taddrs_by_account: HashMap<
        AccountId,
        HashMap<u32, TransparentAddressSyncInfo<AccountId>>,
    > = HashMap::new();
    for taddr in taddrs.iter() {
        let account = taddr.account_id;
        taddrs_by_account
            .entry(account)
            .or_default()
            .insert(taddr.index, taddr.clone());
    }

    let mut added = Vec::new();

    // For each account, loop over each address index, filling in any missing ones, so long as
    // we have transactions for them or have not yet reached the gap limit.
    for (account, taddrs_in_account) in taddrs_by_account.iter() {
        let mut index = 0;
        let mut consecutive_unused_addresses = 0;
        while consecutive_unused_addresses < TADDR_INDEX_GAP_LIMIT {
            match taddrs_in_account.get(&index) {
                None => {
                    let ua = db.put_address_with_diversifier_index(account, index.into())?;
                    if let Some(taddr) = ua.transparent() {
                        let sync_info = TransparentAddressSyncInfo {
                            account_id: *account,
                            index,
                            address: *taddr,
                            height: None,
                            used: false,
                        };
                        added.push(sync_info.clone());
                        taddrs.push(sync_info);
                    }

                    consecutive_unused_addresses += 1;
                }
                Some(addr_info) => {
                    // Has this address been used?
                    if addr_info.used {
                        consecutive_unused_addresses = 0;
                    } else {
                        consecutive_unused_addresses += 1;
                    }
                }
            }

            index += 1;
        }
    }

    Ok(added)
}

/// Calculates the fee for some transaction.
///
/// Returns `Error::OutPointMissing` if any UTXO consumed by the transaction is not already in the `utxos` table.
fn calculate_transaction_fee(transaction: Transaction, conn: &Connection) -> Result<Amount, Error> {
    fn get_prevout_value(outpoint: &OutPoint, conn: &Connection) -> Result<Amount, Error> {
        Ok(Amount::try_from(
            conn.query_row(
                "SELECT value_zat FROM utxos WHERE prevout_txid = :txid AND prevout_idx = :idx",
                named_params! {
                    ":txid": outpoint.hash(),
                    ":idx": outpoint.n(),
                },
                |row| row.get::<_, i64>(0),
            )
            .map_err(|e| match e {
                rusqlite::Error::QueryReturnedNoRows => Error::OutPointMissing,
                e => e.into(),
            })?,
        )
        .unwrap())
    }

    let transparent_value_balance = transaction
        .transparent_bundle()
        .map_or(Ok(Amount::zero()), |b| {
            b.value_balance(|outpoint| get_prevout_value(outpoint, conn))
        })?;
    let sprout_value_balance = transaction.sprout_bundle().map_or(Amount::zero(), |b| {
        b.value_balance().unwrap_or(Amount::zero())
    });
    let sapling_value_balance = transaction.sapling_value_balance();
    let orchard_value_balance = transaction
        .orchard_bundle()
        .map_or(Amount::zero(), |b| b.value_balance().to_owned());

    Ok((transparent_value_balance
        + sprout_value_balance
        + sapling_value_balance
        + orchard_value_balance)
        .unwrap())
}

/// Initializes the fee column for every transaction that is missing it (and that have blocks that have been downloaded).
fn initialize_transaction_fees(db: &mut Db, conn: &Connection) -> Result<(), Error> {
    conn.prepare("SELECT txid FROM transactions WHERE fee IS NULL AND block IS NOT NULL")?
        .query_map([], |r| r.get::<_, [u8; 32]>(0).map(TxId::from_bytes))?
        .try_for_each(|txid| {
            let txid = txid?;
            let tx = db
                .data
                .get_transaction(txid)?
                .ok_or(Error::Internal("Transaction not found.".to_string()))?;

            // Some fees we'll fail to calculate because we're missing UTXOs.
            // that should only happen when it's an incoming transparent transaction from a spent UTXO,
            // but if it's incoming, the user didn't pay the fee anyway so it's not a big deal to not display the fee.
            // If we want to predict whether transactions in the mempool have a ZIP-317 sufficient fee, we'll have to
            // add a way to fetch those UTXO values.
            if let Ok(fee) = calculate_transaction_fee(tx, conn) {
                let txid: [u8; 32] = txid.into();
                conn.execute(
                    "UPDATE transactions SET fee = :fee WHERE txid = :txid",
                    named_params! {
                        ":fee": i64::from(fee),
                        ":txid": txid,
                    },
                )?;
            }
            Ok::<_, Error>(())
        })?;
    Ok(())
}

async fn download_full_shielded_transactions<P: AsRef<Path> + Clone>(
    client: &mut CompactTxStreamerClient<Channel>,
    data_file: P,
    db: &mut Db,
    network: &Network,
    cancellation_token: CancellationToken,
) -> Result<Vec<TxId>, Error> {
    let client = Arc::new(Mutex::new(client));
    // Scope the database connection so it's closed before we use the db argument,
    // to avoid 'database is locked' errors.
    let txids;
    {
        let conn = Connection::open(data_file.clone())?;
        let mut stmt = conn.prepare("SELECT txid FROM transactions WHERE raw IS NULL")?;
        txids = stmt
            .query_map([], |r| r.get::<_, [u8; 32]>(0).map(TxId::from_bytes))?
            .collect::<Result<Vec<_>, _>>()?;
    }

    for txid in txids.iter() {
        let raw_tx = webrequest_with_retry(
            || async {
                Ok(client
                    .lock()
                    .await
                    .get_transaction(TxFilter {
                        hash: txid.as_ref().to_vec(),
                        ..Default::default()
                    })
                    .await?
                    .into_inner())
            },
            cancellation_token.clone(),
        )
        .await?;

        // The consensus branch ID passed in here does not matter:
        // - v4 and below cache it internally, but all we do with this transaction while
        //   it is in memory is decryption and serialization, neither of which use the
        //   consensus branch ID.
        // - v5 and above transactions ignore the argument, and parse the correct value
        //   from their encoding.
        let tx = Transaction::read(raw_tx.data.reader(), BranchId::Sapling)?;
        decrypt_and_store_transaction(network, &mut db.data, &tx)?;
    }

    Ok(txids)
}

async fn update_subtree_roots<P: Parameters>(
    client: &mut CompactTxStreamerClient<Channel>,
    db_data: &mut WalletDb<rusqlite::Connection, P>,
) -> Result<(), anyhow::Error> {
    // Update sapling subtree roots
    let mut request = service::GetSubtreeRootsArg::default();
    request.set_shielded_protocol(service::ShieldedProtocol::Sapling);
    let roots: Vec<CommitmentTreeRoot<sapling::Node>> = client
        .get_subtree_roots(request)
        .await?
        .into_inner()
        .and_then(|root| async move {
            let root_hash = sapling::Node::read(&root.root_hash[..])?;
            Ok(CommitmentTreeRoot::from_parts(
                BlockHeight::from_u32(root.completing_block_height as u32),
                root_hash,
            ))
        })
        .try_collect()
        .await?;
    db_data.put_sapling_subtree_roots(0, &roots)?;

    // Update orchard subtree roots
    request = service::GetSubtreeRootsArg::default();
    request.set_shielded_protocol(service::ShieldedProtocol::Orchard);
    let roots: Vec<CommitmentTreeRoot<MerkleHashOrchard>> = client
        .get_subtree_roots(request)
        .await?
        .into_inner()
        .and_then(|root| async move {
            let root_hash = MerkleHashOrchard::read(&root.root_hash[..])?;
            Ok(CommitmentTreeRoot::from_parts(
                BlockHeight::from_u32(root.completing_block_height as u32),
                root_hash,
            ))
        })
        .try_collect()
        .await?;
    db_data.put_orchard_subtree_roots(0, roots.as_slice())?;

    Ok(())
}

async fn download_transparent_transactions(
    client: &mut CompactTxStreamerClient<Channel>,
    db: &mut Db,
    network: &Network,
    address: &TransparentAddress,
    start: Option<BlockHeight>,
    end: BlockHeight,
    cancellation_token: CancellationToken,
) -> Result<Vec<TxId>, Error> {
    let mut txids_for_new_transparent_transactions = vec![];
    let client = Arc::new(Mutex::new(client));
    let transparent_transactions = webrequest_with_retry(
        || async {
            client
                .lock()
                .await
                .get_taddress_txids(TransparentAddressBlockFilter {
                    address: address.encode(network),
                    range: Some(BlockRange {
                        start: Some(BlockId {
                            height: start
                                .unwrap_or_else(|| {
                                    network.activation_height(NetworkUpgrade::Sapling).unwrap()
                                })
                                .into(),
                            ..Default::default()
                        }),
                        end: Some(BlockId {
                            height: end.into(),
                            ..Default::default()
                        }),
                    }),
                })
                .await?
                .into_inner()
                .try_collect::<Vec<_>>()
                .await
        },
        cancellation_token,
    )
    .await?;
    for rawtx in transparent_transactions {
        let height = BlockHeight::from_u32(rawtx.height as u32);
        let tx = Transaction::read(&rawtx.data[..], BranchId::for_height(network, height))?;
        txids_for_new_transparent_transactions.push(tx.txid());

        // Record spends.
        decrypt_and_store_transaction(network, &mut db.data, &tx)?;

        // Record receives.
        if let Some(t) = tx.transparent_bundle() {
            for (txout_index, txout) in t.vout.iter().enumerate() {
                let outpoint = OutPoint::new(tx.txid().as_ref().to_owned(), txout_index as u32);
                if let Some(output) =
                    WalletTransparentOutput::from_parts(outpoint, txout.to_owned(), height)
                {
                    match db.data.put_received_transparent_utxo(&output) {
                        Ok(_) => (),
                        Err(SqliteClientError::AddressNotRecognized(_)) => (), // Not all tx outputs will be for our wallet. We only want to record the ones that are.
                        Err(x) => return Err(x.into()),
                    };
                }
            }
        }
    }

    db.data
        .put_latest_scanned_block_for_transparent(address, BlockHeight::from_u32(end.into()))?;

    Ok(txids_for_new_transparent_transactions)
}

#[derive(Debug, Clone)]
struct SyncState {
    network: Network,
    progress: Arc<Option<Box<dyn SyncUpdate>>>,
    min_confirmations: u32,
    cancellation_token: CancellationToken,
}

async fn download_and_scan_blocks(
    client: &mut CompactTxStreamerClient<Channel>,
    mut db: Db,
    state: &SyncState,
    status: Option<&SyncUpdateData>,
    block_range: &ScanRange,
) -> Result<DownloadAndScanResult, Error> {
    info!("Received instructions to download [{})", block_range);
    let (send, mut receive) =
        mpsc::channel::<(Vec<CompactBlock>, ChainState)>(CHUNK_CHANNEL_CAPACITY);
    let priorities_changed_token = state.cancellation_token.child_token();

    // Download the blocks in `scan_range` into the block source, overwriting any
    // existing blocks in this range.
    let mut client = client.to_owned();
    let downloader_block_range = block_range.clone();
    let downloader_priorities_changed_token = priorities_changed_token.clone();
    let downloader = tokio::spawn(async move {
        download_blocks(
            &mut client,
            &downloader_block_range,
            send,
            downloader_priorities_changed_token,
        )
        .await
    });

    let state = state.clone();
    let mut status = status.cloned();
    let scanner_block_range = block_range.clone();
    let scanner = tokio::spawn(async move {
        let mut priorities_changed = false;
        while let Some((chunk, chain_state)) = select! {
            result = receive.recv() => Ok(result),
            _ = state.cancellation_token.cancelled() => Err(Error::Canceled),
        }? {
            let scan_range = ScanRange::from_parts(
                chunk.first().unwrap().height()..chunk.last().unwrap().height() + 1,
                scanner_block_range.priority(),
            );

            info!("Scanning {} blocks [{}).", chunk.len(), scan_range);

            // Insert the blocks into the block cache.
            db.blocks.insert_range(chunk);

            if scan_blocks(&state.network, &mut db, &scan_range, &chain_state)?
                && !priorities_changed
            {
                // Notify the downloader to break out early because we'll be getting a new range request.
                // But we don't abort here. Presumably the original scan range is still interesting
                // (just less so), so don't throw away what we've already downloaded.
                priorities_changed_token.cancel();
                priorities_changed = true;
                info!("Resetting...");
            }

            // Now that they've been scanned, we don't need them any more.
            db.blocks.remove_range(scan_range.block_range());

            if let Some(s) = status.as_mut() {
                s.current_step += scan_range.len() as u64;
                update_and_report_status(
                    s,
                    &db.data,
                    state.min_confirmations,
                    state.progress.borrow(),
                )?;
            }

            if state.cancellation_token.is_cancelled() {
                return Err(Error::Canceled);
            }
        }

        Ok::<_, Error>(DownloadAndScanResult {
            priorities_changed,
            status,
        })
    });

    let (_, scan_result) = tokio::try_join!(downloader, scanner)?;

    scan_result
}

async fn download_blocks(
    client: &mut CompactTxStreamerClient<Channel>,
    scan_range: &ScanRange,
    sender: mpsc::Sender<(Vec<CompactBlock>, ChainState)>,
    cancellation_token: CancellationToken,
) -> Result<(), Status> {
    info!("Fetching {}", scan_range);
    let mut start = service::BlockId::default();
    start.height = scan_range.block_range().start.into();
    let mut end = service::BlockId::default();
    end.height = (scan_range.block_range().end - 1).into();
    let range = service::BlockRange {
        start: Some(start),
        end: Some(end),
    };

    let mut blocks = Vec::new();
    let mut stream = client.get_block_range(range).await?.into_inner();
    let mut accumulated_size = 0;
    while let Some(block) = stream.try_next().await? {
        // Process each block here
        accumulated_size += block.vtx.iter().fold(0, |acc, tx| {
            acc + tx.actions.len() + tx.outputs.len() + tx.spends.len()
        });
        blocks.push(block);

        if accumulated_size > BLOCKS_CHUNK_THRESHOLD {
            send_blocks_and_chainstate(client, blocks, &sender).await?;
            blocks = Vec::new();
            accumulated_size = 0;
        }

        if cancellation_token.is_cancelled() {
            info!("Breaking out of download loop due to cancellation.");
            break;
        }
    }

    info!(
        "Block download exiting. Cancelled? {}",
        cancellation_token.is_cancelled()
    );

    if !blocks.is_empty() {
        send_blocks_and_chainstate(client, blocks, &sender).await?;
    }

    async fn send_blocks_and_chainstate(
        client: &mut CompactTxStreamerClient<Channel>,
        blocks: Vec<CompactBlock>,
        sender: &mpsc::Sender<(Vec<CompactBlock>, ChainState)>,
    ) -> Result<(), Status> {
        let base_height = blocks[0].height - 1;
        let tree_state = client
            .get_tree_state(service::BlockId {
                height: base_height,
                ..Default::default()
            })
            .await?
            .into_inner();
        let chain_state = tree_state.to_chain_state()?;

        sender.send((blocks, chain_state)).await.unwrap();

        Ok(())
    }

    Ok(())
}

/// Scans the given block range and checks for scanning errors that indicate the wallet's
/// chain tip is out of sync with blockchain history.
///
/// Returns `true` if scanning these blocks materially changed the suggested scan ranges.
fn scan_blocks(
    network: &Network,
    db: &mut Db,
    scan_range: &ScanRange,
    chain_state: &ChainState,
) -> Result<bool, Error> {
    let scan_result = scan_cached_blocks(
        network,
        &db.blocks,
        &mut db.data,
        scan_range.block_range().start,
        chain_state,
        scan_range.len(),
    );

    // Check for scanning errors that indicate that the wallet's chain tip is out of
    // sync with blockchain history.
    match scan_result {
        Ok(_) => {
            // If scanning these blocks caused a suggested range to be added that has a
            // higher priority than the current range, invalidate the current ranges.
            let latest_ranges = db.data.suggest_scan_ranges()?;

            Ok(if let Some(range) = latest_ranges.first() {
                range.priority() > scan_range.priority()
            } else {
                false
            })
        }
        Err(ChainError::Scan(err)) if err.is_continuity_error() => {
            // Pick a height to rewind to, which must be at least one block before
            // the height at which the error occurred, but may be an earlier height
            // determined based on heuristics such as the platform, available bandwidth,
            // size of recent CompactBlocks, etc.
            let rewind_height = err.at_height().saturating_sub(10);
            info!(
                "Chain reorg detected at {}, rewinding to {}",
                err.at_height(),
                rewind_height,
            );

            // Rewind to the chosen height.
            db.data.truncate_to_height(rewind_height)?;

            // Delete cached blocks from rewind_height onwards.
            //
            // This does imply that assumed-valid blocks will be re-downloaded, but it
            // is also possible that in the intervening time, a chain reorg has
            // occurred that orphaned some of those blocks.
            db.blocks.truncate_to_height(rewind_height);

            Ok(true)
        }
        Err(other) => Err(other.into()),
    }
}

async fn watch_mempool(client: &mut CompactTxStreamerClient<Channel>) -> Result<(), Error> {
    let mut response = client.get_mempool_stream(Empty {}).await?.into_inner();

    while let Some(_tx) = response.message().await? {}

    Ok(())
}

pub(crate) fn get_transactions(
    db: &mut Db,
    conn: &mut rusqlite::Connection,
    network: &Network,
    account_id_filter: Option<u32>,
    starting_block_filter: Option<u32>,
    ending_block_filter: Option<u32>,
) -> Result<Vec<crate::interop::Transaction>, Error> {
    let ufvkeys = db.data.get_unified_full_viewing_keys()?;

    rusqlite::vtab::array::load_module(conn)?;

    let mut stmt_txs = conn.prepare(GET_TRANSACTIONS_SQL)?;

    let rows = stmt_txs.query_and_then(
        named_params! {
            ":account_id": account_id_filter,
            ":starting_block": starting_block_filter,
            ":ending_block": ending_block_filter,
        },
        |row| -> Result<crate::interop::Transaction, Error> {
            let account_id: u32 = row.get("account_id")?;
            let output_pool: u32 = row.get("output_pool")?;
            let from_account_id: Option<u32> = row.get("from_account_id")?;
            let to_account_id: Option<u32> = row.get("to_account_id")?;
            let mut recipient: Option<String> = row.get("to_address")?;
            let value: u64 = row.get("value")?;
            let memo: Option<Vec<u8>> = row.get("memo")?;
            let memo = memo.unwrap_or_default();

            let ufvk = ufvkeys.get(&AccountId::from(account_id));

            // Work out the receiving address when the sqlite db doesn't record it
            // but we have a diversifier that can regenerate it.
            if recipient.is_none() {
                let diversifier: Option<Vec<u8>> = row.get("diversifier")?;
                if let Some(diversifier) = diversifier {
                    recipient = match output_pool {
                        2 => ufvk.and_then(|k| {
                            k.sapling().and_then(|s| {
                                s.diversified_address(sapling::keys::Diversifier(
                                    diversifier.try_into().unwrap(),
                                ))
                                .map(|a| a.encode(network))
                            })
                        }),
                        3 => ufvk.and_then(|k| {
                            k.orchard().map(|o| {
                                UnifiedAddress::from_receivers(
                                    Some(o.address(
                                        orchard::keys::Diversifier::from_bytes(
                                            diversifier.try_into().unwrap(),
                                        ),
                                        Scope::External,
                                    )),
                                    None,
                                    None,
                                )
                                .unwrap()
                                .encode(network)
                            })
                        }),
                        _ => None,
                    }
                }
            }

            let mut tx = crate::interop::Transaction {
                account_id,
                txid: row.get::<_, Vec<u8>>("txid")?,
                mined_height: row.get("mined_height")?,
                expired_unmined: row
                    .get::<_, Option<bool>>("expired_unmined")?
                    .unwrap_or(false),
                block_time: match row.get::<_, Option<i64>>("block_time")? {
                    Some(v) => Some(
                        time::OffsetDateTime::from_unix_timestamp(v)
                            .map_err(|e| {
                                Error::SqliteClient(SqliteClientError::CorruptedData(format!(
                                    "Error translating unix timestamp: {}",
                                    e
                                )))
                            })?
                            .into(),
                    ),
                    None => None,
                },
                fee: row.get::<_, Option<u64>>("fee_paid")?,
                account_balance_delta: row.get("account_balance_delta")?,
                incoming: Vec::new(),
                outgoing: Vec::new(),
                change: Vec::new(),
            };

            let note = TransactionNote {
                value,
                recipient: recipient.clone().unwrap(),
                memo: if memo.is_empty() {
                    None
                } else {
                    Some(memo.clone())
                },
            };

            // We establish change by all the following criteria holding true:
            // * the recipient is to the same account
            // * the recipient is shielded (since change will never be sent to the transparent pool).
            // * the memo does not contain user text,
            let is_change = to_account_id == from_account_id
                && output_pool > 1
                && Memo::from_bytes(&memo).is_ok_and(|m| !matches!(m, Memo::Text(_)));

            if is_change {
                tx.change.push(note);
            } else {
                // A 'send to self' will appear as both incoming and outgoing.
                if to_account_id == Some(account_id) {
                    tx.incoming.push(note.clone());
                }

                if from_account_id == Some(account_id) {
                    tx.outgoing.push(note);
                }
            }

            Ok(tx)
        },
    )?;

    let mut result: Vec<crate::interop::Transaction> = Vec::new();
    for row_result in rows {
        let mut row = row_result?;

        let last = result.last();
        let add = last.is_some() && last.unwrap().txid.eq(&row.txid);
        if add {
            // This row adds line items to the last transaction.
            // Pop it off the list to change it, then we'll add it back.
            let mut tx = result.pop().unwrap();
            tx.incoming.append(&mut row.incoming);
            tx.outgoing.append(&mut row.outgoing);
            tx.change.append(&mut row.change);
            result.push(tx);
        } else {
            result.push(row);
        }
    }

    Ok(result)
}

#[cfg(test)]
mod tests {
    use zcash_client_backend::data_api::Account;
    use zcash_primitives::transaction::components::Amount;

    use crate::test_constants::setup_test;

    use super::*;

    #[tokio_shared_rt::test(flavor = "multi_thread")]
    async fn test_sync() {
        let mut setup = setup_test().await;
        let (seed, birthday, account_id, _) = setup.create_account().await.unwrap();

        // Add one more account two accounts with the same seed leads to accounts with unique indexes and thus spending authorities.
        let account_ids = [
            account_id,
            setup
                .db
                .add_account(
                    &seed,
                    zip32::AccountId::ZERO.next().unwrap(),
                    birthday,
                    &mut setup.client,
                )
                .await
                .unwrap()
                .0
                .id(),
        ]
        .map(|a| a);

        let result = sync(
            setup.server_uri.clone(),
            &setup.data_file,
            None,
            setup.db_init.min_confirmations,
            false,
            CancellationToken::new(),
        )
        .await
        .unwrap();

        info!("Tip: {:?}", result.last_fully_scanned_block);

        if let Some(summary) = setup
            .db
            .data
            .get_wallet_summary(setup.db_init.min_confirmations)
            .unwrap()
        {
            for id in account_ids {
                info!("Account index: {}", u32::from(id));
                let b = summary.account_balances().get(&id).unwrap();
                info!(
                    "Orchard balance: {}",
                    format_zec(Amount::from(b.orchard_balance().spendable_value()))
                );
                info!(
                    "Sapling balance: {}",
                    format_zec(Amount::from(b.sapling_balance().spendable_value()))
                );
                info!("Transparent balance: {}", format_zec(b.unshielded()));
            }
        } else {
            info!("No summary found");
        }

        let mut conn = Connection::open(setup.db_init.data_file).unwrap();
        let txs =
            get_transactions(&mut setup.db, &mut conn, &setup.network, None, None, None).unwrap();
        assert_eq!(txs.len(), 0);
    }

    const COIN: u64 = 1_0000_0000;

    fn format_zec(value: impl Into<Amount>) -> String {
        let value = i64::from(value.into());
        let abs_value = value.unsigned_abs();
        let abs_zec = abs_value / COIN;
        let frac = abs_value % COIN;
        let zec = if value.is_negative() {
            -(abs_zec as i64)
        } else {
            abs_zec as i64
        };
        format!("{:3}.{:08} ZEC", zec, frac)
    }
}
