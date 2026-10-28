use futures_util::{StreamExt, TryStreamExt, stream};
use http::Uri;
use orchard::{keys::Scope, tree::MerkleHashOrchard};
use prost::bytes::Buf;
use rusqlite::{Connection, OptionalExtension, named_params};
use std::{
    borrow::Borrow,
    collections::{BTreeSet, HashMap},
    future::Future,
    ops::Range,
    path::Path,
    sync::Arc,
};
use tokio::{select, sync::mpsc};
use tokio_util::sync::CancellationToken;
use tonic::{Status, transport::Channel};
use tracing::{debug, info};
use uniffi::deps::anyhow;
use uuid::Uuid;
use zcash_client_sqlite::{AccountUuid, WalletDb, error::SqliteClientError, util::SystemClock};
use zcash_keys::address::UnifiedAddress;
use zcash_primitives::{
    block::BlockHash,
    merkle_tree::HashSer,
    transaction::{Transaction, TxId},
};
use zcash_transparent::{address::TransparentAddress, bundle::OutPoint};

use zcash_client_backend::{
    data_api::{
        TransactionDataRequest, TransactionStatus, TransparentAddressSyncInfo,
        WalletCommitmentTrees, WalletRead, WalletWrite,
        chain::{ChainState, CommitmentTreeRoot, CompactChainState, scan_cached_blocks},
        scanning::{ScanPriority, ScanRange},
        wallet::{ConfirmationsPolicy, decrypt_and_store_transaction},
    },
    encoding::AddressCodec,
    proto::{
        compact_formats::CompactBlock,
        service::{
            self, Empty, RawTransaction, TransparentAddressBlockFilter, TxFilter,
            compact_tx_streamer_client::CompactTxStreamerClient,
        },
    },
    wallet::WalletTransparentOutput,
};
use zcash_protocol::{
    PoolType,
    consensus::{BlockHeight, BranchId, Network, Parameters},
    memo::Memo,
    value::{ZatBalance, Zatoshis},
};

use crate::{
    backing_store::{Db, WalletRng, wallet_scan_lock},
    block_source::BlockCacheError,
    blockrange::BlockRange,
    error::Error,
    grpc::get_client,
    interop::{Pool, SyncUpdate, SyncUpdateData, TransactionNote},
    lightclient::parse_network,
    resilience::webrequest_with_retry,
    sql_statements::{GET_OUTPOINT_VALUE, GET_TRANSACTIONS_SQL},
    sync_timing::{Phase, RangeGuard, SYNC_TIMING, SyncTiming},
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

/// Maximum number of in-flight lightwalletd reads for one ordered batch.
///
/// Gap-limit rounds and scan ranges stay serial. Only independent reads inside one round overlap,
/// and their results are still applied in the previous order.
const RPC_CONCURRENCY: usize = 8;

pub async fn sync<P: AsRef<Path>>(
    uri: Uri,
    data_file: P,
    progress: Option<Box<dyn SyncUpdate>>,
    confirmations_policy: ConfirmationsPolicy,
    continually: bool,
    cancellation_token: CancellationToken,
) -> Result<SyncUpdateData, Error> {
    let timing = SyncTiming::from_env();
    let _guard = timing.finish_guard();
    SYNC_TIMING
        .scope(
            timing,
            sync_scoped(
                uri,
                data_file,
                progress,
                confirmations_policy,
                continually,
                cancellation_token,
            ),
        )
        .await
}

async fn sync_scoped<P: AsRef<Path>>(
    uri: Uri,
    data_file: P,
    progress: Option<Box<dyn SyncUpdate>>,
    confirmations_policy: ConfirmationsPolicy,
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
    let network = parse_network(&info)?;
    let mut db = Db::load(&data_file, network)?;
    let conn = Connection::open(&data_file)?;
    // Automatic mode skips note commitment trees only when every stored account is view-only.
    // An empty wallet still maintains trees, so a later spending account keeps the old path.
    let view_only = !db.data.maintains_note_commitment_trees()?;
    let mut state = SyncState {
        cancellation_token,
        confirmations_policy,
        network,
        progress: Arc::new(progress),
        timing: SyncTiming::current(),
        view_only,
    };

    // 1) Download note commitment tree data from lightwalletd
    // 2) Pass the commitment tree data to the database.
    // A view-only scan never appends those trees, so the subtree-root round trips are skipped.
    if view_only {
        info!("View-only wallet; skipping note commitment subtree roots");
    } else {
        update_subtree_roots(&mut client.clone(), &mut db.data, &state.timing).await?;
    }

    let mut status = SyncUpdateData {
        current_step: 0,
        total_steps: 0,
        last_fully_scanned_block: None,
        tip_height: 0,
        last_error: None,
    };

    loop {
        // An account imported while a continual scan is running can change whether this
        // wallet maintains trees. Re-read that before each pass and, when a spending
        // account appears, load the subtree roots this pass's `ChainState` scan needs.
        ensure_tree_maintenance(&mut client, &mut db, &mut state).await?;

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
        .map_err(|e| Error::Internal(format!("Invalid block height: {e}")))?;

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

        // Download transparent transactions before shielded scanning.
        // Each gap-limit round stays serial, because the next addresses exist only after this
        // round's results are applied. Within a round, txid fetches overlap up to
        // RPC_CONCURRENCY and are applied in the same address order as a sequential scan.
        let mut taddrs = db.data.get_transparent_addresses_and_sync_heights()?;
        let mut taddrs_to_scan = taddrs.clone();
        while !taddrs_to_scan.is_empty() {
            let mut planned = Vec::new();
            // A height past the tip used to fail inside the sequential loop, after earlier
            // addresses in that round had already been stored. Collect the valid prefix, apply
            // it, then return the same error.
            let mut height_error = None;
            for (index, addr_info) in taddrs.iter().enumerate() {
                if !taddrs_to_scan.contains(addr_info) {
                    continue;
                }
                // In a weird case, I've seen the addr_info.height be greater than the tip_height, so we need to check for that.
                let range =
                    match BlockRange::try_from(addr_info.height..(status.tip_height + 1).into()) {
                        Ok(range) => range,
                        Err(_) => {
                            height_error = Some(Error::Internal(format!(
                                "T address {:?} has sync height {}, which exceeds tip height {}.",
                                addr_info.address, addr_info.height, status.tip_height
                            )));
                            break;
                        }
                    };
                if range.is_empty() {
                    continue;
                }
                planned.push(PlannedTransparent {
                    index,
                    address: addr_info.address,
                    range,
                });
            }

            let fetches = planned.into_iter().map(|job| {
                let fetch_client = client.clone();
                let network = state.network;
                let cancellation_token = state.cancellation_token.clone();
                async move {
                    let raw_transactions = fetch_transparent_transactions(
                        fetch_client,
                        network,
                        job.address,
                        &job.range,
                        cancellation_token,
                    )
                    .await?;
                    Ok::<_, Error>(FetchedTransparent {
                        index: job.index,
                        address: job.address,
                        range: job.range,
                        raw_transactions,
                    })
                }
            });

            apply_in_order_bounded(fetches, RPC_CONCURRENCY, |fetched| {
                let txids = store_transparent_transactions(
                    &mut db,
                    &state.network,
                    &fetched.address,
                    &fetched.range,
                    fetched.raw_transactions,
                )?;
                if !txids.is_empty() {
                    taddrs[fetched.index].used = true;
                    report_new_transactions(
                        txids,
                        state.progress.borrow(),
                        &data_file,
                        &mut db,
                        &conn,
                        state.network,
                    )?;
                }
                Ok(())
            })
            .await?;

            if let Some(error) = height_error {
                return Err(error);
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
                    // Hold the wallet scan lock from the tree-mode check through the commit.
                    // A spending-key upgrade waits for this attempt, then records its rescan
                    // against every block this attempt stored. The check also has to happen
                    // before each retry: `CommitmentTreeRequired` stays inside this loop.
                    let _range = open_range(&state.timing, scan_range);
                    let scan_lock = wallet_scan_lock(data_file.as_ref());
                    let scan_result = {
                        let _guard = scan_lock.lock().await;
                        ensure_tree_maintenance(&mut client, &mut db, &mut state).await?;
                        download_and_scan_blocks(
                            &mut client,
                            Db::load(&data_file, state.network)?,
                            &state,
                            None,
                            scan_range,
                        )
                        .await?
                    };
                    drop(_range);

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

        update_and_report_status(&mut status, &db.data, confirmations_policy, &state.progress)?;

        let mut caught_up = true;
        for scan_range in scan_ranges.into_iter() {
            let _range = open_range(&state.timing, &scan_range);
            let scan_lock = wallet_scan_lock(data_file.as_ref());
            let scan_result = {
                let _guard = scan_lock.lock().await;
                // Re-read tree maintenance under the same lock the upgrade takes, so this
                // chunk cannot commit a view-only scan after the spending flag is stored.
                ensure_tree_maintenance(&mut client, &mut db, &mut state).await?;
                download_and_scan_blocks(
                    &mut client,
                    Db::load(&data_file, state.network)?,
                    &state,
                    Some(&status),
                    &scan_range,
                )
                .await?
            };
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
            drop(_range);

            // Report all transactions that are in the block range we just scanned,
            // even if we didn't just download them (which would have only included shielded transactions).
            // Transparent transactions in this range only just now got assigned their block height,
            // so reporting them (again) at this point is good for the client.
            report_transactions_in_range(
                scan_range.block_range(),
                None,
                &state.progress,
                &data_file,
                &mut db,
                Some(&conn),
                &state.network,
            )?;

            update_and_report_status(&mut status, &db.data, confirmations_policy, &state.progress)?;

            if scan_result.priorities_changed {
                // The suggested scan ranges have been updated (either due to a continuity
                // error or because a higher priority range has been added).
                caught_up = false;
                break;
            }
        }

        let mut satisfied_requests = BTreeSet::new();
        loop {
            let mut new_request_encountered = false;
            for data_request in db.data.transaction_data_requests()? {
                if satisfied_requests.contains(&data_request) {
                    continue;
                } else {
                    new_request_encountered = true;
                }

                info!("Fetching data for request {:?}", data_request);
                match &data_request {
                    TransactionDataRequest::GetStatus(txid) => {
                        let _timer = state.timing.start(Phase::TxEnhance);
                        let status = fetch_transaction(
                            &mut client,
                            &state.network,
                            status.tip_height.into(),
                            *txid,
                        )
                        .await?
                        .map_or(
                            TransactionStatus::TxidNotRecognized,
                            |(_, mined_height)| {
                                mined_height.map_or(
                                    TransactionStatus::NotInMainChain,
                                    TransactionStatus::Mined,
                                )
                            },
                        );
                        info!("Got status {:?}", status);
                        db.data.set_transaction_status(*txid, status)?;
                    }
                    TransactionDataRequest::Enhancement(txid) => {
                        let _timer = state.timing.start(Phase::TxEnhance);
                        match fetch_transaction(
                            &mut client,
                            &state.network,
                            status.tip_height.into(),
                            *txid,
                        )
                        .await?
                        {
                            None => {
                                info!("Txid not recognized {:?}", txid);
                                db.data.set_transaction_status(
                                    *txid,
                                    TransactionStatus::TxidNotRecognized,
                                )?;
                            }
                            Some((tx, mined_height)) => {
                                info!(
                                    "Enhancing tx {:?} with mined height {:?}",
                                    txid, mined_height
                                );
                                decrypt_and_store_transaction(
                                    &state.network,
                                    &mut db.data,
                                    &tx,
                                    mined_height,
                                )?;
                            }
                        }
                    }
                    TransactionDataRequest::TransactionsInvolvingAddress(tia) => {
                        let _timer = state.timing.start(Phase::TransparentTxids);
                        let address = tia.address().encode(&state.network);
                        let request = service::TransparentAddressBlockFilter {
                            address: address.clone(),
                            range: Some(service::BlockRange {
                                start: Some(service::BlockId {
                                    height: u64::from(tia.block_range_start()),
                                    ..Default::default()
                                }),
                                end: tia.block_range_end().map(|h| service::BlockId {
                                    height: u64::from(h - 1), // `BlockRange` end is inclusive.
                                    ..Default::default()
                                }),
                                pool_types: vec![],
                            }),
                        };

                        let mut stream = client.get_taddress_txids(request).await?.into_inner();
                        while let Some(raw_tx) = stream.next().await {
                            let (tx, mined_height) = parse_raw_transaction(
                                &state.network,
                                status.tip_height.into(),
                                raw_tx?,
                            )?;
                            info!(
                                "Found tx {:?} for address {} with mined height {:?}",
                                tx.txid(),
                                address,
                                mined_height
                            );
                            decrypt_and_store_transaction(
                                &state.network,
                                &mut db.data,
                                &tx,
                                mined_height,
                            )?
                        }
                    }
                }

                satisfied_requests.insert(data_request);
            }

            if !new_request_encountered {
                break;
            }
        }

        if caught_up {
            update_status(&mut status, &db.data, confirmations_policy)?;

            if !continually {
                return Ok(status);
            }

            state.timing.emit("sync pass");
            report_status(&status, &state.progress);

            // We'll loop around again when the next block is mined.
            // Eventually we should actually do something with the transactions in the mempool too.
            // WARNING: This is vulnerable to a race condition, because if a new block has *already* been mined
            // but not noticed above, we'll end up waiting for yet *another* block to be mined.
            let _idle = state.timing.begin_idle();
            select! {
                _ = state.cancellation_token.cancelled() => Err(Status::cancelled("Request cancelled")),
                _ = watch_mempool(&state.network, &mut client, &data_file, &mut db, &state.progress) => Ok(()),
            }?;
        }
    }
}

fn report_transactions_in_range<P: AsRef<Path>>(
    range: &Range<BlockHeight>,
    only_txid: Option<TxId>,
    progress: &Option<Box<dyn SyncUpdate>>,
    data_file: &P,
    db: &mut Db,
    conn: Option<&Connection>,
    network: &Network,
) -> Result<(), Error> {
    if let Some(conn) = conn {
        initialize_transaction_fees(db, conn)?;
    }
    if let Some(sink) = progress.as_ref() {
        let mut conn = Connection::open(data_file)?;
        let transactions = get_transactions(
            db,
            &mut conn,
            network,
            None,
            Some(range.start.into()),
            Some((range.end - 1).into()),
        )?;
        let transactions_to_report = {
            if let Some(txid) = only_txid {
                let txid = txid.as_ref().to_vec();
                transactions
                    .iter()
                    .filter(|r| txid == r.txid)
                    .cloned()
                    .collect::<Vec<_>>()
            } else {
                transactions
            }
        };
        if !transactions_to_report.is_empty() {
            sink.report_transactions(transactions_to_report);
        }
    }

    Ok(())
}

fn update_status<'a>(
    status: &'a mut SyncUpdateData,
    data: &WalletDb<Connection, Network, SystemClock, WalletRng>,
    confirmations_policy: ConfirmationsPolicy,
) -> Result<&'a SyncUpdateData, Error> {
    status.last_fully_scanned_block = data.block_fully_scanned()?.map(|b| b.block_height().into());

    // Disabled for now because it's unstable -- it goes backwards, jumps around, etc.
    if false
        && let Some(wallet_progress) = data
            .get_wallet_summary(confirmations_policy)
            .unwrap_or(None)
            .and_then(|s| s.progress().recovery())
    {
        status.current_step = *wallet_progress.numerator();
        status.total_steps = *wallet_progress.denominator();
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
    data: &WalletDb<Connection, Network, SystemClock, WalletRng>,
    confirmations_policy: ConfirmationsPolicy,
    progress: &Option<Box<dyn SyncUpdate>>,
) -> Result<(), Error> {
    report_status(update_status(status, data, confirmations_policy)?, progress);
    Ok(())
}

struct DownloadAndScanResult {
    priorities_changed: bool,
    status: Option<SyncUpdateData>,
}

fn fill_in_taddrs_to_gap_limit(
    taddrs: &mut Vec<TransparentAddressSyncInfo<AccountUuid>>,
    db: &mut WalletDb<Connection, Network, SystemClock, WalletRng>,
) -> Result<Vec<TransparentAddressSyncInfo<AccountUuid>>, Error> {
    // Transform the vector of addresses into this structure:
    // Account -> (DiversifierIndex -> TransparentAddressSyncInfo)
    let mut taddrs_by_account: HashMap<
        AccountUuid,
        HashMap<u32, TransparentAddressSyncInfo<AccountUuid>>,
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
        let birthday_height = db.get_account_birthday(account.to_owned())?;
        while consecutive_unused_addresses < TADDR_INDEX_GAP_LIMIT {
            match taddrs_in_account.get(&index) {
                None => {
                    let ua =
                        db.put_address_with_diversifier_index(account.to_owned(), index.into())?;
                    if let Some(taddr) = ua.transparent() {
                        let sync_info = TransparentAddressSyncInfo {
                            account_id: *account,
                            index,
                            address: *taddr,
                            height: birthday_height,
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
fn calculate_transaction_fee(
    transaction: Transaction,
    conn: &Connection,
) -> Result<ZatBalance, Error> {
    fn get_prevout_value(
        outpoint: &OutPoint,
        conn: &Connection,
    ) -> Result<Option<Zatoshis>, Error> {
        match conn.query_row(
            GET_OUTPOINT_VALUE,
            named_params! {
                ":txid": outpoint.hash(),
                ":idx": outpoint.n(),
            },
            |row| row.get::<_, u64>(0),
        ) {
            Ok(v) => Ok(Some(Zatoshis::try_from(v).map_err(|e| {
                Error::Internal(format!("Invalid ZAT value in UTXO table: {e}"))
            })?)),
            Err(rusqlite::Error::QueryReturnedNoRows) => Ok(None),
            Err(e) => Err(e.into()),
        }
    }

    let transparent_value_balance =
        transaction
            .transparent_bundle()
            .map_or(Ok(ZatBalance::zero()), |b| {
                b.value_balance(|outpoint| get_prevout_value(outpoint, conn))?
                    .ok_or(Error::OutPointMissing)
            })?;
    let sprout_value_balance = transaction.sprout_bundle().map_or(ZatBalance::zero(), |b| {
        b.value_balance().unwrap_or(ZatBalance::zero())
    });
    let sapling_value_balance = transaction.sapling_value_balance();
    let orchard_value_balance = transaction
        .orchard_bundle()
        .map_or(ZatBalance::zero(), |b| b.value_balance().to_owned());

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

    let network = *network;
    let fetches = txids.iter().copied().map(|txid| {
        let fetch_client = client.clone();
        let cancellation_token = cancellation_token.clone();
        async move {
            let raw_tx = {
                let _timer = SyncTiming::current().start(Phase::TxEnhance);
                webrequest_with_retry(
                    || {
                        let mut fetch_client = fetch_client.clone();
                        let hash = txid.as_ref().to_vec();
                        async move {
                            Ok(fetch_client
                                .get_transaction(TxFilter {
                                    hash,
                                    ..Default::default()
                                })
                                .await?
                                .into_inner())
                        }
                    },
                    cancellation_token,
                )
                .await?
            };

            // The consensus branch ID passed in here does not matter:
            // - v4 and below cache it internally, but all we do with this transaction while
            //   it is in memory is decryption and serialization, neither of which use the
            //   consensus branch ID.
            // - v5 and above transactions ignore the argument, and parse the correct value
            //   from their encoding.
            let tx = Transaction::read(raw_tx.data.reader(), BranchId::Sapling)?;
            Ok::<_, Error>((txid, tx, BlockHeight::from_u32(raw_tx.height as u32)))
        }
    });

    // Fetches overlap. Decrypt-and-store stays in txid order so a failure still leaves the
    // same prefix of transactions that a sequential loop would have committed.
    apply_in_order_bounded(fetches, RPC_CONCURRENCY, |(_txid, tx, height)| {
        decrypt_and_store_transaction(&network, &mut db.data, &tx, Some(height))?;
        Ok(())
    })
    .await?;

    Ok(txids)
}

async fn download_subtree_roots<H: HashSer + Send>(
    client: &mut CompactTxStreamerClient<Channel>,
    protocol: service::ShieldedProtocol,
    timing: &SyncTiming,
) -> Result<Vec<CommitmentTreeRoot<H>>, anyhow::Error> {
    let mut request = service::GetSubtreeRootsArg::default();
    request.set_shielded_protocol(protocol);
    let _timer = timing.start(Phase::TreeState);
    Ok(client
        .get_subtree_roots(request)
        .await?
        .into_inner()
        .and_then(|root| async move {
            let root_hash = H::read(&root.root_hash[..])?;
            Ok(CommitmentTreeRoot::from_parts(
                BlockHeight::from_u32(root.completing_block_height as u32),
                root_hash,
            ))
        })
        .try_collect()
        .await?)
}

async fn update_subtree_roots<P: Parameters>(
    client: &mut CompactTxStreamerClient<Channel>,
    db_data: &mut WalletDb<rusqlite::Connection, P, SystemClock, WalletRng>,
    timing: &SyncTiming,
) -> Result<(), anyhow::Error> {
    // Subtree roots are a contiguous prefix from index 0. Scan ranges run chain-tip before
    // historic, and the chain-tip range can complete a later shard while the historic range
    // still has to write an earlier one. An empty shard table accepts that later shard; the
    // earlier insert then fails with SubtreeDiscontinuity. Sapling and Orchard were already
    // preloaded. Ironwood uses the same hash type as Orchard and has to be preloaded too.
    let sapling_roots =
        download_subtree_roots::<sapling::Node>(client, service::ShieldedProtocol::Sapling, timing)
            .await?;
    {
        let _timer = timing.start(Phase::DbWrite);
        db_data
            .put_sapling_subtree_roots(0, &sapling_roots)
            .map_err(|e| {
                anyhow::anyhow!("sapling subtree roots ({}): {e:?}", sapling_roots.len())
            })?;
    }

    let orchard_roots = download_subtree_roots::<MerkleHashOrchard>(
        client,
        service::ShieldedProtocol::Orchard,
        timing,
    )
    .await?;
    {
        let _timer = timing.start(Phase::DbWrite);
        db_data
            .put_orchard_subtree_roots(0, &orchard_roots)
            .map_err(|e| {
                anyhow::anyhow!("orchard subtree roots ({}): {e:?}", orchard_roots.len())
            })?;
    }

    let ironwood_roots = download_subtree_roots::<MerkleHashOrchard>(
        client,
        service::ShieldedProtocol::Ironwood,
        timing,
    )
    .await?;
    {
        let _timer = timing.start(Phase::DbWrite);
        db_data
            .put_ironwood_subtree_roots(0, &ironwood_roots)
            .map_err(|e| {
                anyhow::anyhow!("ironwood subtree roots ({}): {e:?}", ironwood_roots.len())
            })?;
    }

    info!(
        "Loaded subtree roots sapling={} orchard={} ironwood={}",
        sapling_roots.len(),
        orchard_roots.len(),
        ironwood_roots.len()
    );

    Ok(())
}

struct PlannedTransparent {
    index: usize,
    address: TransparentAddress,
    range: BlockRange,
}

struct FetchedTransparent {
    index: usize,
    address: TransparentAddress,
    range: BlockRange,
    raw_transactions: Vec<RawTransaction>,
}

async fn fetch_transparent_transactions(
    client: CompactTxStreamerClient<Channel>,
    network: Network,
    address: TransparentAddress,
    range: &BlockRange,
    cancellation_token: CancellationToken,
) -> Result<Vec<RawTransaction>, Error> {
    let _timer = SyncTiming::current().start(Phase::TransparentTxids);
    let transactions = webrequest_with_retry(
        || {
            let mut client = client.clone();
            let address = address.encode(&network);
            let range = range.into();
            async move {
                client
                    .get_taddress_txids(TransparentAddressBlockFilter {
                        address,
                        range: Some(range),
                    })
                    .await?
                    .into_inner()
                    .try_collect::<Vec<_>>()
                    .await
            }
        },
        cancellation_token,
    )
    .await?;
    Ok(transactions)
}

fn store_transparent_transactions(
    db: &mut Db,
    network: &Network,
    address: &TransparentAddress,
    range: &BlockRange,
    raw_transactions: Vec<RawTransaction>,
) -> Result<Vec<TxId>, Error> {
    let mut txids = Vec::new();
    for rawtx in raw_transactions {
        let height = BlockHeight::from_u32(rawtx.height as u32);
        let tx = Transaction::read(&rawtx.data[..], BranchId::for_height(network, height))?;
        txids.push(tx.txid());

        // Record spends.
        decrypt_and_store_transaction(network, &mut db.data, &tx, Some(height))?;

        // Record receives.
        if let Some(t) = tx.transparent_bundle() {
            for (txout_index, txout) in t.vout.iter().enumerate() {
                let outpoint = OutPoint::new(tx.txid().as_ref().to_owned(), txout_index as u32);
                if let Some(output) = WalletTransparentOutput::from_parts(
                    outpoint,
                    txout.to_owned(),
                    Some(height),
                    None,
                    None,
                    None,
                ) {
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
        .put_latest_scanned_block_for_transparent(address, range.end())?;

    Ok(txids)
}

/// Polls `jobs` with at most `concurrency` futures in flight and applies each success in input order.
///
/// The first error is returned after every earlier success has been applied. Later futures are
/// dropped, which cancels in-flight lightwalletd reads. A concurrency of 0 runs one at a time.
async fn apply_in_order_bounded<T, E, Fut>(
    jobs: impl IntoIterator<Item = Fut>,
    concurrency: usize,
    mut apply: impl FnMut(T) -> Result<(), E>,
) -> Result<(), E>
where
    Fut: Future<Output = Result<T, E>>,
{
    let mut incoming = std::pin::pin!(stream::iter(jobs).buffered(concurrency.max(1)));
    while let Some(item) = incoming.next().await {
        apply(item?)?;
    }
    Ok(())
}

#[derive(Debug, Clone)]
struct SyncState {
    network: Network,
    progress: Arc<Option<Box<dyn SyncUpdate>>>,
    confirmations_policy: ConfirmationsPolicy,
    cancellation_token: CancellationToken,
    timing: SyncTiming,
    /// `true` when this wallet scans without note commitment trees.
    view_only: bool,
}

/// Prior-block continuity passed from the downloader to `scan_cached_blocks`.
enum PriorBlock {
    /// Frontiers from `GetTreeState`, for a wallet that appends note commitment trees.
    Spending(ChainState),
    /// Height, hash, and end-of-block tree sizes taken from the compact block itself.
    ViewOnly(CompactChainState),
}

fn open_range(timing: &SyncTiming, scan_range: &ScanRange) -> RangeGuard {
    if !timing.is_enabled() {
        return timing.begin_range(0, 0, "");
    }
    timing.begin_range(
        u32::from(scan_range.block_range().start),
        u32::from(scan_range.block_range().end),
        &format!("{:?}", scan_range.priority()),
    )
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
        mpsc::channel::<(Vec<CompactBlock>, PriorBlock)>(CHUNK_CHANNEL_CAPACITY);
    let priorities_changed_token = state.cancellation_token.child_token();

    // Download the blocks in `scan_range` into the block source, overwriting any
    // existing blocks in this range.
    let mut client = client.to_owned();
    let downloader_block_range = block_range.clone();
    let downloader_priorities_changed_token = priorities_changed_token.clone();
    let downloader_timing = state.timing.clone();
    let downloader_view_only = state.view_only;
    let downloader = tokio::spawn(async move {
        download_blocks(
            &mut client,
            &downloader_block_range,
            send,
            downloader_priorities_changed_token,
            downloader_timing,
            downloader_view_only,
        )
        .await
    });

    let state = state.clone();
    let mut status = status.cloned();
    let scanner_block_range = block_range.clone();
    let scanner = tokio::spawn(async move {
        let mut priorities_changed = false;
        while let Some((chunk, prior)) = select! {
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

            if scan_blocks(&state.network, &mut db, &scan_range, &prior, &state.timing)?
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
                    state.confirmations_policy,
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

    let (downloaded, scanned) = tokio::try_join!(downloader, scanner)?;
    // `JoinHandle`'s success value is the task result. A `GetTreeState` or compact-block
    // failure would otherwise be dropped while the scanner saw a closed channel and returned Ok.
    downloaded?;
    scanned
}

async fn download_blocks(
    client: &mut CompactTxStreamerClient<Channel>,
    scan_range: &ScanRange,
    sender: mpsc::Sender<(Vec<CompactBlock>, PriorBlock)>,
    cancellation_token: CancellationToken,
    timing: SyncTiming,
    view_only: bool,
) -> Result<(), Status> {
    info!("Fetching {}", scan_range);
    let mut start = service::BlockId::default();
    start.height = scan_range.block_range().start.into();
    let mut end = service::BlockId::default();
    end.height = (scan_range.block_range().end - 1).into();
    let range = service::BlockRange {
        start: Some(start),
        end: Some(end),
        pool_types: vec![],
    };

    // The first chunk's tree state is the block before the scan range, so it can be in flight
    // for the whole compact-block stream. Later chunks arm a fetch once their first block arrives.
    // Chunks are still sent in order. A prefetch is used only when its height matches the chunk.
    // View-only scans read continuity from the compact block and do not call `GetTreeState`.
    let start_height: u64 = scan_range.block_range().start.into();
    let mut prefetch = if view_only {
        None
    } else {
        start_height
            .checked_sub(1)
            .map(|height| PrefetchedTreeState::spawn(client.clone(), height, timing.clone()))
    };

    let mut blocks = Vec::new();
    let mut download = timing.start(Phase::BlockDownload);
    let mut stream = client.get_block_range(range).await?.into_inner();
    let mut accumulated_size = 0;
    while let Some(block) = stream.try_next().await? {
        if blocks.is_empty() && !view_only {
            arm_tree_state_prefetch(&mut prefetch, client, block.height, &timing);
        }
        // Process each block here
        accumulated_size += block.vtx.iter().fold(0, |acc, tx| {
            acc + tx.actions.len() + tx.outputs.len() + tx.spends.len()
        });
        blocks.push(block);

        if accumulated_size > BLOCKS_CHUNK_THRESHOLD {
            // Tree-state fetches and channel backpressure are not block-download time.
            download.pause();
            send_blocks_and_chainstate(client, blocks, &sender, &timing, &mut prefetch, view_only)
                .await?;
            blocks = Vec::new();
            accumulated_size = 0;
            download.resume();
        }

        if cancellation_token.is_cancelled() {
            info!("Breaking out of download loop due to cancellation.");
            break;
        }
    }
    drop(download);

    info!(
        "Block download exiting. Cancelled? {}",
        cancellation_token.is_cancelled()
    );

    if !blocks.is_empty() {
        send_blocks_and_chainstate(client, blocks, &sender, &timing, &mut prefetch, view_only)
            .await?;
    }

    Ok(())
}

/// A `GetTreeState` round trip started before the chunk that needs it is ready to send.
///
/// Dropping the prefetch aborts the task so a discarded or cancelled fetch does not stay running.
struct PrefetchedTreeState {
    height: u64,
    handle: tokio::task::JoinHandle<Result<ChainState, Status>>,
}

impl PrefetchedTreeState {
    fn spawn(
        mut client: CompactTxStreamerClient<Channel>,
        height: u64,
        timing: SyncTiming,
    ) -> Self {
        let handle =
            tokio::spawn(async move { fetch_tree_state(&mut client, height, &timing).await });
        Self { height, handle }
    }

    async fn into_chain_state(mut self) -> Result<ChainState, Status> {
        // Poll through `self` so dropping this future still aborts the task.
        let joined = (&mut self.handle).await;
        match joined {
            Ok(result) => result,
            Err(error) if error.is_cancelled() => {
                Err(Status::cancelled("tree state prefetch cancelled"))
            }
            Err(error) => Err(Status::internal(format!(
                "tree state prefetch failed: {error}"
            ))),
        }
    }
}

impl Drop for PrefetchedTreeState {
    fn drop(&mut self) {
        self.handle.abort();
    }
}

fn arm_tree_state_prefetch(
    prefetch: &mut Option<PrefetchedTreeState>,
    client: &CompactTxStreamerClient<Channel>,
    block_height: u64,
    timing: &SyncTiming,
) {
    // Height 0 has no prior block. Leave that case to the send path, which keeps the
    // previous wrapping subtraction instead of inventing a different base height.
    let Some(base_height) = block_height.checked_sub(1) else {
        *prefetch = None;
        return;
    };
    if prefetch
        .as_ref()
        .is_some_and(|existing| existing.height == base_height)
    {
        return;
    }
    *prefetch = Some(PrefetchedTreeState::spawn(
        client.clone(),
        base_height,
        timing.clone(),
    ));
}

async fn fetch_tree_state(
    client: &mut CompactTxStreamerClient<Channel>,
    height: u64,
    timing: &SyncTiming,
) -> Result<ChainState, Status> {
    let _timer = timing.start(Phase::TreeState);
    let tree_state = client
        .get_tree_state(service::BlockId {
            height,
            ..Default::default()
        })
        .await?
        .into_inner();
    Ok(tree_state.to_chain_state()?)
}

async fn send_blocks_and_chainstate(
    client: &mut CompactTxStreamerClient<Channel>,
    blocks: Vec<CompactBlock>,
    sender: &mpsc::Sender<(Vec<CompactBlock>, PriorBlock)>,
    timing: &SyncTiming,
    prefetch: &mut Option<PrefetchedTreeState>,
    view_only: bool,
) -> Result<(), Status> {
    let prior = if view_only {
        PriorBlock::ViewOnly(compact_chain_state_before(&blocks[0])?)
    } else {
        PriorBlock::Spending(chain_state_for_blocks(client, &blocks, timing, prefetch).await?)
    };
    sender.send((blocks, prior)).await.unwrap();
    Ok(())
}

/// Prior-block continuity for a view-only scan, taken from the first compact block in the chunk.
///
/// The compact block's chain metadata is the tree size at the end of that block. Subtracting the
/// block's commitment count yields the size at the end of the previous block. The previous
/// block's hash is `prev_hash`. No `GetTreeState` round trip is required.
fn compact_chain_state_before(block: &CompactBlock) -> Result<CompactChainState, Status> {
    if block.height == 0 {
        return Err(Status::failed_precondition(
            "view-only scan cannot start at height 0",
        ));
    }
    if block.prev_hash.len() != 32 && block.header.is_empty() {
        return Err(Status::internal(format!(
            "compact block {} has no prev_hash",
            block.height
        )));
    }
    let metadata = block.chain_metadata.as_ref().ok_or_else(|| {
        Status::internal(format!(
            "compact block {} has no chain metadata",
            block.height
        ))
    })?;
    let sapling_outputs = commitment_count(block, |tx| tx.outputs.len())?;
    let orchard_actions = commitment_count(block, |tx| tx.actions.len())?;
    let ironwood_actions = commitment_count(block, |tx| tx.ironwood_actions.len())?;
    Ok(CompactChainState::from_parts(
        block.height().saturating_sub(1),
        block.prev_hash(),
        metadata
            .sapling_commitment_tree_size
            .checked_sub(sapling_outputs)
            .ok_or_else(|| {
                Status::internal(format!(
                    "sapling tree size underflow at block {}",
                    block.height
                ))
            })?,
        metadata
            .orchard_commitment_tree_size
            .checked_sub(orchard_actions)
            .ok_or_else(|| {
                Status::internal(format!(
                    "orchard tree size underflow at block {}",
                    block.height
                ))
            })?,
        metadata
            .ironwood_commitment_tree_size
            .checked_sub(ironwood_actions)
            .ok_or_else(|| {
                Status::internal(format!(
                    "ironwood tree size underflow at block {}",
                    block.height
                ))
            })?,
    ))
}

fn commitment_count(
    block: &CompactBlock,
    count: impl Fn(&zcash_client_backend::proto::compact_formats::CompactTx) -> usize,
) -> Result<u32, Status> {
    block.vtx.iter().try_fold(0u32, |total, tx| {
        let n = u32::try_from(count(tx))
            .map_err(|_| Status::internal("commitment count does not fit in u32"))?;
        total
            .checked_add(n)
            .ok_or_else(|| Status::internal("commitment count overflow"))
    })
}

async fn chain_state_for_blocks(
    client: &mut CompactTxStreamerClient<Channel>,
    blocks: &[CompactBlock],
    timing: &SyncTiming,
    prefetch: &mut Option<PrefetchedTreeState>,
) -> Result<ChainState, Status> {
    // Same base height as the previous sequential fetch, including the height-0 wrap.
    let base_height = blocks[0].height - 1;
    if let Some(prefetched) = prefetch.take() {
        if prefetched.height == base_height {
            return prefetched.into_chain_state().await;
        }
        drop(prefetched);
    }
    fetch_tree_state(client, base_height, timing).await
}

/// Scans the given block range and checks for scanning errors that indicate the wallet's
/// chain tip is out of sync with blockchain history.
///
/// Returns `true` if scanning these blocks materially changed the suggested scan ranges.
fn scan_blocks(
    network: &Network,
    db: &mut Db,
    scan_range: &ScanRange,
    prior: &PriorBlock,
    timing: &SyncTiming,
) -> Result<bool, Error> {
    let scan_result = timing.measure_scan(|| match prior {
        PriorBlock::Spending(chain_state) => scan_cached_blocks(
            network,
            &db.blocks,
            &mut db.data,
            scan_range.block_range().start,
            chain_state,
            scan_range.len(),
        ),
        PriorBlock::ViewOnly(chain_state) => scan_cached_blocks(
            network,
            &db.blocks,
            &mut db.data,
            scan_range.block_range().start,
            chain_state,
            scan_range.len(),
        ),
    });

    // Check for scanning errors that indicate that the wallet's chain tip is out of
    // sync with blockchain history.
    match scan_result {
        Ok(_) => {
            timing.add_blocks(scan_range.len() as u64);
            // If scanning these blocks caused a suggested range to be added that has a
            // higher priority than the current range, invalidate the current ranges.
            let latest_ranges = db.data.suggest_scan_ranges()?;

            Ok(if let Some(range) = latest_ranges.first() {
                range.priority() > scan_range.priority()
            } else {
                false
            })
        }
        Err(ChainError::Scan(
            zcash_client_backend::scanning::ScanError::CommitmentTreeRequired { .. },
        )) => {
            // A spending account was imported after this chunk was fetched without frontiers.
            // Nothing was written. The next pass reloads subtree roots and scans with a ChainState.
            info!(
                "Note commitment trees became required during a view-only scan; reloading subtree roots"
            );
            Ok(true)
        }
        Err(ChainError::Scan(err)) if err.is_continuity_error() => {
            // Pick a height to rewind to, which must be at least one block before
            // the height at which the error occurred, but may be an earlier height
            // determined based on heuristics such as the platform, available bandwidth,
            // size of recent CompactBlocks, etc.
            info!("Chain reorg detected at {}, rewinding", err.at_height(),);
            rewind_for_continuity(db, err.at_height())?;

            Ok(true)
        }
        Err(other) => Err(other.into()),
    }
}

/// Reloads subtree roots when a spending account appears during a view-only sync.
///
/// `view_only` is otherwise fixed for the whole `sync` call, including a continual scan.
/// Importing a spending account on another call makes the database maintain trees, and the
/// next compact-block chunk must carry frontiers or `scan_cached_blocks` returns
/// `CommitmentTreeRequired`.
async fn ensure_tree_maintenance(
    client: &mut CompactTxStreamerClient<Channel>,
    db: &mut Db,
    state: &mut SyncState,
) -> Result<(), Error> {
    let maintains_trees = db.data.maintains_note_commitment_trees()?;
    if maintains_trees && state.view_only {
        info!("A spending account is in the wallet; loading note commitment subtree roots");
        update_subtree_roots(client, &mut db.data, &state.timing).await?;
    }
    state.view_only = !maintains_trees;
    Ok(())
}

/// Rewinds about ten blocks before a continuity error.
///
/// A spending wallet uses `truncate_to_height`, which needs a note commitment checkpoint.
/// A view-only wallet uses `rewind_view_only` and the hash of a stored block. The preferred
/// target is ten blocks before the error. If that block was never stored, the newest stored
/// block below the error is used instead. When no stored block is below the error, this
/// returns an error instead of reporting a rewind that did not happen.
fn rewind_for_continuity(db: &mut Db, at_height: BlockHeight) -> Result<(), Error> {
    let rewind_height = at_height.saturating_sub(10);
    if db.data.maintains_note_commitment_trees()? {
        db.data.truncate_to_height(rewind_height)?;
        db.blocks.truncate_to_height(rewind_height);
        return Ok(());
    }

    let Some((height, hash)) = view_only_rewind_target(db, rewind_height, at_height)? else {
        return Err(Error::Internal(format!(
            "View-only continuity error at {at_height}, and no stored block below it to rewind to"
        )));
    };
    info!("Chain reorg detected at {at_height}, rewinding view-only wallet to {height}");
    db.data.rewind_view_only(height, hash)?;
    // Delete cached blocks from the recovery height onwards.
    //
    // This does imply that assumed-valid blocks will be re-downloaded, but it
    // is also possible that in the intervening time, a chain reorg has
    // occurred that orphaned some of those blocks.
    db.blocks.truncate_to_height(height);
    Ok(())
}

/// Stored block to rewind a view-only wallet to, strictly below `at_height`.
///
/// Prefers the newest stored block at or below `desired`. If the scan has not reached
/// `desired`, uses the newest stored block that is still below the continuity error.
fn view_only_rewind_target(
    db: &Db,
    desired: BlockHeight,
    at_height: BlockHeight,
) -> Result<Option<(BlockHeight, BlockHash)>, Error> {
    if let Some(found) = newest_stored_block_at_or_below(db, desired)? {
        return Ok(Some(found));
    }
    let Some(found) = newest_stored_block_at_or_below(db, at_height.saturating_sub(1))? else {
        return Ok(None);
    };
    if found.0 >= at_height {
        return Ok(None);
    }
    Ok(Some(found))
}

fn newest_stored_block_at_or_below(
    db: &Db,
    max_height: BlockHeight,
) -> Result<Option<(BlockHeight, BlockHash)>, Error> {
    let conn = Connection::open(&db.path)?;
    let row = conn
        .query_row(
            "SELECT height, hash FROM blocks WHERE height <= :height ORDER BY height DESC LIMIT 1",
            named_params![":height": u32::from(max_height)],
            |row| Ok((row.get::<_, u32>(0)?, row.get::<_, Vec<u8>>(1)?)),
        )
        .optional()?;
    let Some((height, hash)) = row else {
        return Ok(None);
    };
    let hash: [u8; 32] = hash
        .as_slice()
        .try_into()
        .map_err(|_| Error::Internal(format!("stored block hash at {height} is not 32 bytes")))?;
    Ok(Some((BlockHeight::from_u32(height), BlockHash(hash))))
}

async fn watch_mempool<P: AsRef<Path>>(
    network: &Network,
    client: &mut CompactTxStreamerClient<Channel>,
    data_file: &P,
    db: &mut Db,
    progress: &Option<Box<dyn SyncUpdate>>,
) -> Result<(), Error> {
    let mut response = client.get_mempool_stream(Empty {}).await?.into_inner();

    // The primary purpose of this function is to pause until the next block is mined,
    // which will trigger the next iteration of the main loop to download the next block.
    // But while we wait, we'll download transactions from the mempool and report them to the client.
    while let Some(raw_tx) = response.message().await? {
        if progress.is_some() {
            let tx = Transaction::read(raw_tx.data.reader(), BranchId::Sapling)?;
            decrypt_and_store_transaction(
                network,
                &mut db.data,
                &tx,
                Some((raw_tx.height as u32).into()),
            )?;

            // The sql table records mempool transactions with a mined height of 0.
            let mempool_range = Range {
                start: BlockHeight::from_u32(0),
                end: BlockHeight::from_u32(0),
            };
            report_transactions_in_range(
                &mempool_range,
                Some(tx.txid()),
                progress,
                data_file,
                db,
                None,
                network,
            )?;
        }
    }

    Ok(())
}

async fn fetch_transaction(
    client: &mut CompactTxStreamerClient<Channel>,
    params: &Network,
    chain_tip: BlockHeight,
    txid: TxId,
) -> Result<Option<(Transaction, Option<BlockHeight>)>, Error> {
    let request = service::TxFilter {
        hash: txid.as_ref().to_vec(),
        ..Default::default()
    };

    let raw_tx = match client.get_transaction(request).await {
        Ok(response) => Ok(Some(response.into_inner())),
        Err(status) if status.code() == tonic::Code::NotFound => Ok(None),
        // Workaround for https://github.com/ZcashFoundation/zebra/issues/8786 and https://github.com/zcash/lightwalletd/issues/497
        Err(status)
            if status.code() == tonic::Code::Unknown
                && (status.message()
                    == "-5: No such mempool or blockchain transaction. Use gettransaction for wallet transactions."
                    || status.message() == "0: Transaction not found") =>
        {
            Ok(None)
        }
        Err(status) => Err(status),
    }?;

    raw_tx
        .map(|raw_tx| parse_raw_transaction(params, chain_tip, raw_tx))
        .transpose()
}

fn parse_raw_transaction(
    params: &Network,
    chain_tip: BlockHeight,
    tx: RawTransaction,
) -> Result<(Transaction, Option<BlockHeight>), Error> {
    let mined_height = (tx.height > 0 && tx.height <= u64::from(u32::MAX))
        .then(|| BlockHeight::from_u32(u32::try_from(tx.height).unwrap()));

    let tx = Transaction::read(
        &tx.data[..],
        // We assume unmined transactions are created with the current consensus branch ID.
        BranchId::for_height(params, mined_height.unwrap_or(chain_tip)),
    )?;

    Ok((tx, mined_height))
}

/// Returns the transactions that match the given filters.
///
/// `starting_block_filter` and `ending_block_filter` are inclusive.
pub(crate) fn get_transactions(
    db: &mut Db,
    conn: &mut rusqlite::Connection,
    network: &Network,
    account_id_filter: Option<AccountUuid>,
    starting_block_filter: Option<u32>,
    ending_block_filter: Option<u32>,
) -> Result<Vec<crate::interop::Transaction>, Error> {
    let ufvkeys = db.data.get_unified_full_viewing_keys()?;

    rusqlite::vtab::array::load_module(conn)?;

    let mut stmt_txs = conn.prepare(GET_TRANSACTIONS_SQL)?;

    let rows = stmt_txs.query_and_then(
        named_params! {
            ":account_uuid": account_id_filter.map(|id| id.expose_uuid()),
            ":starting_block": starting_block_filter,
            ":ending_block": ending_block_filter,
        },
        |row| -> Result<crate::interop::Transaction, Error> {
            let account_uuid = AccountUuid::from_uuid(row.get("account_uuid")?);
            let output_pool: u32 = row.get("output_pool")?;
            let from_account_uuid = row
                .get::<_, Option<Uuid>>("from_account_uuid")?
                .map(AccountUuid::from_uuid);
            let to_account_uuid = row
                .get::<_, Option<Uuid>>("to_account_uuid")?
                .map(AccountUuid::from_uuid);
            let mut recipient: Option<String> = row.get("to_address")?;
            let value: u64 = row.get("value")?;
            let memo: Option<Vec<u8>> = row.get("memo")?;
            let memo = memo.unwrap_or_default();

            let output_pool = match output_pool {
                0 => PoolType::Transparent,
                2 => PoolType::SAPLING,
                3 => PoolType::ORCHARD,
                _ => {
                    return Err(Error::SqliteClient(SqliteClientError::CorruptedData(
                        format!("Unknown output pool type: {output_pool}"),
                    )));
                }
            };

            let ufvk = ufvkeys.get(&account_uuid);

            // Work out the receiving address when the sqlite db doesn't record it
            // but we have a diversifier that can regenerate it.
            if recipient.is_none() {
                let diversifier: Option<Vec<u8>> = row.get("diversifier")?;
                if let Some(diversifier) = diversifier {
                    recipient = match output_pool {
                        PoolType::SAPLING => ufvk.and_then(|k| {
                            k.sapling().and_then(|s| {
                                s.diversified_address(sapling::keys::Diversifier(
                                    diversifier.try_into().unwrap(),
                                ))
                                .map(|a| a.encode(network))
                            })
                        }),
                        PoolType::ORCHARD => ufvk.and_then(|k| {
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
                account_id: account_uuid.expose_uuid().as_bytes().to_vec(),
                txid: row.get::<_, Vec<u8>>("txid")?,
                mined_height: match row.get("mined_height")? {
                    0 => None, // the db records unmined transactions with a height of 0.
                    h => Some(h),
                },
                expired_unmined: row
                    .get::<_, Option<bool>>("expired_unmined")?
                    .unwrap_or(false),
                block_time: match row.get::<_, Option<i64>>("block_time")? {
                    Some(v) => Some(
                        time::OffsetDateTime::from_unix_timestamp(v)
                            .map_err(|e| {
                                Error::SqliteClient(SqliteClientError::CorruptedData(format!(
                                    "Error translating unix timestamp: {e}"
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
                pool: match output_pool {
                    PoolType::Transparent => Pool::Transparent,
                    PoolType::SAPLING => Pool::Sapling,
                    PoolType::ORCHARD => Pool::Orchard,
                    PoolType::IRONWOOD => Pool::Ironwood,
                },
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
            let is_change = to_account_uuid == from_account_uuid
                && matches!(output_pool, PoolType::Shielded(_))
                && Memo::from_bytes(&memo).is_ok_and(|m| !matches!(m, Memo::Text(_)));

            if is_change {
                tx.change.push(note);
            } else {
                // A 'send to self' will appear as both incoming and outgoing.
                if to_account_uuid == Some(account_uuid) {
                    tx.incoming.push(note.clone());
                }

                if from_account_uuid == Some(account_uuid) {
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
        let add = last.is_some_and(|l| l.account_id == row.account_id && l.txid.eq(&row.txid));
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

    use crate::test_constants::{self, setup_test};

    use super::*;

    /// Samples compact-block shielded density. Ignored so CI does not download chain data.
    ///
    /// ```bash
    /// cargo test -r --lib profile_shielded_density -- --ignored --nocapture --test-threads=1
    /// ```
    #[ignore]
    #[tokio_shared_rt::test(flavor = "multi_thread")]
    async fn profile_shielded_density() {
        for (name, uri) in [
            ("testnet", test_constants::TESTNET_LIGHTSERVER_URI.clone()),
            ("mainnet", test_constants::MAINNET_LIGHTSERVER_URI.clone()),
        ] {
            let mut client = get_client(uri.clone()).await.unwrap();
            let tip = client
                .get_latest_block(service::ChainSpec::default())
                .await
                .unwrap()
                .get_ref()
                .height;
            eprintln!("{name} tip {tip} via {uri}");
            for (label, start) in [
                ("recent", tip.saturating_sub(2_000)),
                ("back-30k", tip.saturating_sub(30_000)),
                ("back-80k", tip.saturating_sub(80_000)),
            ] {
                sample_compact_range(&mut client, name, label, start, start + 1_000).await;
            }
        }
    }

    async fn sample_compact_range(
        client: &mut CompactTxStreamerClient<Channel>,
        network: &str,
        label: &str,
        start: u64,
        end_exclusive: u64,
    ) {
        let range = service::BlockRange {
            start: Some(service::BlockId {
                height: start,
                ..Default::default()
            }),
            end: Some(service::BlockId {
                height: end_exclusive.saturating_sub(1),
                ..Default::default()
            }),
            pool_types: vec![],
        };
        let started = std::time::Instant::now();
        let mut stream = client
            .clone()
            .get_block_range(range)
            .await
            .unwrap()
            .into_inner();
        let mut blocks = 0u64;
        let mut txs = 0usize;
        let mut sapling = 0usize;
        let mut orchard = 0usize;
        let mut bytes = 0usize;
        while let Some(block) = stream.try_next().await.unwrap() {
            blocks += 1;
            bytes += prost::Message::encoded_len(&block);
            txs += block.vtx.len();
            for tx in &block.vtx {
                sapling += tx.spends.len() + tx.outputs.len();
                orchard += tx.actions.len();
            }
        }
        let elapsed = started.elapsed().as_secs_f64();
        let actions = sapling + orchard;
        eprintln!(
            "{network} {label} [{start},{end_exclusive}) blocks={blocks} txs={txs} sapling={sapling} orchard={orchard} actions={actions} bytes={bytes} {:.2}s {:.0} blocks/s {:.2} actions/block",
            elapsed,
            if elapsed == 0.0 {
                0.0
            } else {
                blocks as f64 / elapsed
            },
            if blocks == 0 {
                0.0
            } else {
                actions as f64 / blocks as f64
            },
        );
    }

    /// Times `put_blocks` and `scan_cached_blocks` for one historic shielded sync.
    ///
    /// Ignored so CI does not download chain data. `NERDBANK_ZCASH_TREE_WRITE_BACK=0` selects the
    /// immediate shard-tree writes; leave it unset for the write-back cache. The birthday defaults
    /// to 3487990 on mainnet, the height used for the completing 44k-block profile. Override it
    /// with `NERDBANK_ZCASH_PROFILE_BIRTHDAY`, or set `NERDBANK_ZCASH_PROFILE_BLOCKS` to place the
    /// birthday that many blocks behind the tip. `NERDBANK_ZCASH_PROFILE_MAINNET=1` selects mainnet
    /// (`https://zcash.mysideoftheweb.com:9067/`).
    ///
    /// A mainnet birthday below the current Ironwood shard tip makes `suggest_scan_ranges` scan the
    /// chain-tip shard first. That succeeds only when Ironwood subtree roots were preloaded from
    /// index 0; otherwise the older historic range fails in `put_blocks` with `SubtreeDiscontinuity`
    /// on the Ironwood tree.
    ///
    /// ```bash
    /// NERDBANK_ZCASH_PROFILE_MAINNET=1 \
    ///   cargo test -p nerdbank-zcash-rust --release --lib profile_historic_shielded_sync -- --ignored --nocapture --test-threads=1
    /// ```
    #[ignore]
    #[tokio_shared_rt::test(flavor = "multi_thread")]
    async fn profile_historic_shielded_sync() {
        let blocks_back_override: Option<u64> = std::env::var("NERDBANK_ZCASH_PROFILE_BLOCKS")
            .ok()
            .and_then(|value| value.parse().ok());
        let birthday_override: Option<u64> = std::env::var("NERDBANK_ZCASH_PROFILE_BIRTHDAY")
            .ok()
            .and_then(|value| value.parse().ok());
        let mainnet = std::env::var("NERDBANK_ZCASH_PROFILE_MAINNET")
            .ok()
            .is_some_and(|value| matches!(value.as_str(), "1" | "true" | "yes" | "on"));
        let uri = if mainnet {
            crate::test_constants::MAINNET_LIGHTSERVER_URI.clone()
        } else {
            crate::test_constants::TESTNET_LIGHTSERVER_URI.clone()
        };

        let totals = std::sync::Arc::new(std::sync::Mutex::new(std::collections::HashMap::<
            String,
            std::time::Duration,
        >::new()));
        let subscriber = SpanTimer {
            next_id: std::sync::atomic::AtomicU64::new(1),
            spans: std::sync::Mutex::new(std::collections::HashMap::new()),
            totals: totals.clone(),
        };
        tracing::subscriber::set_global_default(subscriber)
            .expect("span timer could not be installed");

        let mut client = get_client(uri.clone()).await.unwrap();
        let info = client
            .get_lightd_info(service::Empty {})
            .await
            .unwrap()
            .into_inner();
        let network = parse_network(&info).unwrap();
        let tip = info.block_height;
        let birthday = match (birthday_override, blocks_back_override) {
            (Some(height), _) => height,
            (None, Some(blocks_back)) => tip.saturating_sub(blocks_back),
            (None, None) if mainnet => 3_487_990,
            (None, None) => tip.saturating_sub(25_000),
        };
        let write_back = std::env::var("NERDBANK_ZCASH_TREE_WRITE_BACK")
            .ok()
            .is_none_or(|value| {
                !matches!(
                    value.to_ascii_lowercase().as_str(),
                    "0" | "false" | "off" | "no"
                )
            });
        eprintln!(
            "historic profile network={network:?} tip={tip} birthday={birthday} span={} write_back={write_back} uri={uri}",
            tip.saturating_sub(birthday)
        );

        let wallet_dir = testdir::testdir!();
        let data_file = wallet_dir.join("wallet.sqlite");
        let mut db = Db::init(&data_file, network).unwrap();
        let seed = secrecy::SecretVec::new(
            bip0039::Mnemonic::<bip0039::English>::generate(bip0039::Count::Words24)
                .to_seed("")
                .to_vec(),
        );
        db.add_account(
            "historic profile",
            &seed,
            zip32::AccountId::ZERO,
            birthday,
            &mut client,
        )
        .await
        .unwrap();

        let started = std::time::Instant::now();
        let result = sync(
            uri,
            &data_file,
            None,
            ConfirmationsPolicy::default(),
            false,
            CancellationToken::new(),
        )
        .await
        .unwrap();
        let wall = started.elapsed();
        let totals = totals.lock().unwrap();
        let put_blocks = totals.get("put_blocks").copied().unwrap_or_default();
        let scan = totals
            .get("scan_cached_blocks")
            .copied()
            .unwrap_or_default();
        let scan_without_writes = scan.saturating_sub(put_blocks);
        let outside_scan = wall.saturating_sub(scan);
        let rows = totals.get("put_blocks_rows").copied().unwrap_or_default();
        let build = totals
            .get("put_blocks_build_subtrees")
            .copied()
            .unwrap_or_default();
        let trees = totals
            .get("put_blocks_update_trees")
            .copied()
            .unwrap_or_default();
        let notify = totals.get("put_blocks_notify").copied().unwrap_or_default();
        eprintln!(
            "historic profile done tip={} fully_scanned={:?} wall={:.3}s put_blocks={:.3}s scan_cached_blocks={:.3}s scan_without_put_blocks={:.3}s outside_scan={:.3}s rows={:.3}s build_subtrees={:.3}s update_trees={:.3}s notify={:.3}s",
            result.tip_height,
            result.last_fully_scanned_block,
            wall.as_secs_f64(),
            put_blocks.as_secs_f64(),
            scan.as_secs_f64(),
            scan_without_writes.as_secs_f64(),
            outside_scan.as_secs_f64(),
            rows.as_secs_f64(),
            build.as_secs_f64(),
            trees.as_secs_f64(),
            notify.as_secs_f64(),
        );
    }

    /// Records the wall time spent inside named tracing spans.
    ///
    /// `scan_cached_blocks` stays entered across the nested `put_blocks` span, so its total
    /// includes the writes. Subtract `put_blocks` for the scan-only portion.
    struct SpanTimer {
        next_id: std::sync::atomic::AtomicU64,
        spans: std::sync::Mutex<std::collections::HashMap<u64, OpenSpan>>,
        totals: std::sync::Arc<
            std::sync::Mutex<std::collections::HashMap<String, std::time::Duration>>,
        >,
    }

    struct OpenSpan {
        name: String,
        entered: Option<std::time::Instant>,
    }

    impl tracing::Subscriber for SpanTimer {
        fn enabled(&self, metadata: &tracing::Metadata<'_>) -> bool {
            matches!(
                metadata.name(),
                "put_blocks"
                    | "scan_cached_blocks"
                    | "put_blocks_rows"
                    | "put_blocks_build_subtrees"
                    | "put_blocks_update_trees"
                    | "put_blocks_notify"
            )
        }

        fn new_span(&self, attrs: &tracing::span::Attributes<'_>) -> tracing::span::Id {
            let id = self
                .next_id
                .fetch_add(1, std::sync::atomic::Ordering::Relaxed)
                .max(1);
            self.spans.lock().unwrap().insert(
                id,
                OpenSpan {
                    name: attrs.metadata().name().to_owned(),
                    entered: None,
                },
            );
            tracing::span::Id::from_u64(id)
        }

        fn record(&self, _span: &tracing::span::Id, _values: &tracing::span::Record<'_>) {}

        fn record_follows_from(&self, _span: &tracing::span::Id, _follows: &tracing::span::Id) {}

        fn event(&self, _event: &tracing::Event<'_>) {}

        fn enter(&self, span: &tracing::span::Id) {
            if let Some(state) = self.spans.lock().unwrap().get_mut(&span.into_u64()) {
                state.entered = Some(std::time::Instant::now());
            }
        }

        fn exit(&self, span: &tracing::span::Id) {
            let mut spans = self.spans.lock().unwrap();
            if let Some(state) = spans.get_mut(&span.into_u64())
                && let Some(start) = state.entered.take()
            {
                let elapsed = start.elapsed();
                *self
                    .totals
                    .lock()
                    .unwrap()
                    .entry(state.name.clone())
                    .or_default() += elapsed;
            }
        }
    }

    fn orchard_subtree_root(index: u32) -> CommitmentTreeRoot<MerkleHashOrchard> {
        let mut bytes = [0u8; 32];
        bytes[0] = u8::try_from(index).unwrap_or(u8::MAX);
        bytes[1] = u8::try_from(index >> 8).unwrap_or(0);
        let hash = MerkleHashOrchard::from_bytes(&bytes).expect("canonical Pallas encoding");
        CommitmentTreeRoot::from_parts(BlockHeight::from_u32(1_000 + index * 100), hash)
    }

    fn shard_span(path: &Path, table: &str) -> (i64, i64, i64) {
        let conn = Connection::open(path).unwrap();
        conn.query_row(
            &format!(
                "SELECT COALESCE(MIN(shard_index), -1), COALESCE(MAX(shard_index), -1), COUNT(*) \
                 FROM {table}"
            ),
            [],
            |row| Ok((row.get(0)?, row.get(1)?, row.get(2)?)),
        )
        .unwrap()
    }

    fn assert_contiguous_from_zero(path: &Path, table: &str) {
        let (min, max, count) = shard_span(path, table);
        assert!(
            count > 0 && min == 0 && count == max - min + 1,
            "{table} min={min} max={max} count={count}"
        );
    }

    /// An empty Ironwood shard table accepts whatever shard a chain-tip scan writes first.
    /// A later historic scan that completes an earlier shard then hits `SubtreeDiscontinuity`.
    /// Preloading a prefix from index 0 makes that earlier insert an overlap instead.
    #[test]
    fn ironwood_subtree_roots_from_index_zero_avoid_discontinuity() {
        let wallet_dir = testdir::testdir!();
        let data_file = wallet_dir.join("gapped.sqlite");
        let mut gapped = Db::init(&data_file, Network::MainNetwork).unwrap();
        gapped
            .data
            .put_ironwood_subtree_roots(6, &[orchard_subtree_root(6)])
            .unwrap();
        let err = gapped
            .data
            .put_ironwood_subtree_roots(1, &[orchard_subtree_root(1)])
            .unwrap_err();
        let message = format!("{err:?}");
        assert!(
            message.contains("SubtreeDiscontinuity")
                && message.contains("attempted_insertion_range: 1..2")
                && message.contains("existing_range: 6..7"),
            "{message}"
        );

        let preloaded_file = wallet_dir.join("preloaded.sqlite");
        let mut preloaded = Db::init(&preloaded_file, Network::MainNetwork).unwrap();
        let prefix: Vec<_> = (0..8).map(orchard_subtree_root).collect();
        preloaded
            .data
            .put_ironwood_subtree_roots(0, &prefix)
            .unwrap();
        preloaded
            .data
            .put_ironwood_subtree_roots(1, &[orchard_subtree_root(1)])
            .unwrap();
        assert_contiguous_from_zero(&preloaded_file, "ironwood_tree_shards");
    }

    /// Sync must store Ironwood subtree roots as a contiguous prefix from index 0 before scanning.
    /// Mainnet has completed Ironwood shards; omitting this preload is what made a fresh wallet
    /// with a birthday ~80_000 blocks back fail after the chain-tip range.
    #[tokio_shared_rt::test(flavor = "multi_thread")]
    async fn update_subtree_roots_preloads_ironwood_from_index_zero() {
        for (label, uri, require_ironwood) in [
            (
                "testnet",
                test_constants::TESTNET_LIGHTSERVER_URI.clone(),
                false,
            ),
            (
                "mainnet",
                test_constants::MAINNET_LIGHTSERVER_URI.clone(),
                true,
            ),
        ] {
            let mut client = get_client(uri).await.unwrap();
            let info = client
                .get_lightd_info(service::Empty {})
                .await
                .unwrap()
                .into_inner();
            let network = parse_network(&info).unwrap();
            // `testdir!()` is one directory per test, so each server needs its own file.
            // Reusing the testnet wallet would make the mainnet root insert conflict.
            let data_file = testdir::testdir!().join(format!("{label}.sqlite"));
            let mut db = Db::init(&data_file, network).unwrap();
            update_subtree_roots(&mut client, &mut db.data, &SyncTiming::disabled())
                .await
                .unwrap();

            assert_contiguous_from_zero(&data_file, "sapling_tree_shards");
            assert_contiguous_from_zero(&data_file, "orchard_tree_shards");
            let (min, max, count) = shard_span(&data_file, "ironwood_tree_shards");
            eprintln!("{label} ironwood shards min={min} max={max} count={count}");
            if require_ironwood || count > 0 {
                assert!(
                    count > 0 && min == 0 && count == max - min + 1,
                    "{label} ironwood shards min={min} max={max} count={count}"
                );
            }
        }
    }

    #[tokio_shared_rt::test(flavor = "multi_thread")]
    async fn test_sync() {
        let mut setup = setup_test().await;
        let (seed, birthday, account_id, _) = setup.create_account().await.unwrap();

        // Add one more account. Two accounts with the same seed leads to accounts with unique indexes and thus spending authorities.
        let second_account_zip32_idx = zip32::AccountId::ZERO.next().unwrap();
        let account_ids = [
            (zip32::AccountId::ZERO, account_id),
            (
                second_account_zip32_idx,
                setup
                    .db
                    .add_account(
                        "test account name",
                        &seed,
                        second_account_zip32_idx,
                        birthday,
                        &mut setup.client,
                    )
                    .await
                    .unwrap()
                    .0
                    .id(),
            ),
        ]
        .map(|a| a);

        let result = sync(
            setup.server_uri.clone(),
            &setup.data_file,
            None,
            ConfirmationsPolicy::default(),
            false,
            CancellationToken::new(),
        )
        .await
        .unwrap();

        info!("Tip: {:?}", result.last_fully_scanned_block);

        if let Some(summary) = setup
            .db
            .data
            .get_wallet_summary(ConfirmationsPolicy::default())
            .unwrap()
        {
            for (zip32_index, uuid) in account_ids {
                info!("Account index: {}", u32::from(zip32_index));
                let b = summary.account_balances().get(&uuid).unwrap();
                info!(
                    "Orchard balance: {}",
                    format_zec(b.orchard_balance().spendable_value())
                );
                info!(
                    "Sapling balance: {}",
                    format_zec(b.sapling_balance().spendable_value())
                );
                info!(
                    "Transparent balance: {}",
                    format_zec(b.unshielded_balance().total())
                );
            }
        } else {
            info!("No summary found");
        }

        let mut conn = Connection::open(setup.db_init.data_file).unwrap();
        let txs =
            get_transactions(&mut setup.db, &mut conn, &setup.network, None, None, None).unwrap();
        assert_eq!(txs.len(), 0);
    }

    #[tokio_shared_rt::test(flavor = "multi_thread")]
    async fn bounded_apply_preserves_order_and_concurrency() {
        use std::sync::atomic::{AtomicUsize, Ordering};

        const LIMIT: usize = 4;
        let in_flight = Arc::new(AtomicUsize::new(0));
        let max_in_flight = Arc::new(AtomicUsize::new(0));
        let jobs = (0..20).map({
            let in_flight = Arc::clone(&in_flight);
            let max_in_flight = Arc::clone(&max_in_flight);
            move |index| {
                let in_flight = Arc::clone(&in_flight);
                let max_in_flight = Arc::clone(&max_in_flight);
                async move {
                    let now = in_flight.fetch_add(1, Ordering::SeqCst) + 1;
                    max_in_flight.fetch_max(now, Ordering::SeqCst);
                    tokio::task::yield_now().await;
                    in_flight.fetch_sub(1, Ordering::SeqCst);
                    Ok::<usize, &'static str>(index)
                }
            }
        });

        let mut applied = Vec::new();
        apply_in_order_bounded(jobs, LIMIT, |index| {
            applied.push(index);
            Ok(())
        })
        .await
        .unwrap();

        assert_eq!(applied, (0..20).collect::<Vec<_>>());
        let max = max_in_flight.load(Ordering::SeqCst);
        assert!(max <= LIMIT, "in flight {max}");
        assert!(
            max > 1,
            "expected overlapping fetches, max in flight was {max}"
        );
    }

    #[tokio_shared_rt::test(flavor = "multi_thread")]
    async fn bounded_apply_stops_at_first_error() {
        let jobs =
            (0..6).map(|index| async move { if index == 1 { Err("stop") } else { Ok(index) } });

        let mut applied = Vec::new();
        let error = apply_in_order_bounded(jobs, 8, |index| {
            applied.push(index);
            Ok(())
        })
        .await
        .unwrap_err();

        assert_eq!(error, "stop");
        assert_eq!(applied, vec![0]);
    }

    #[tokio_shared_rt::test(flavor = "multi_thread")]
    async fn bounded_apply_stops_when_apply_fails() {
        let jobs = (0..5).map(|index| async move { Ok::<_, &'static str>(index) });
        let mut applied = Vec::new();
        let error = apply_in_order_bounded(jobs, 2, |index| {
            if index == 2 {
                return Err("apply");
            }
            applied.push(index);
            Ok(())
        })
        .await
        .unwrap_err();

        assert_eq!(error, "apply");
        assert_eq!(applied, vec![0, 1]);
    }

    #[test]
    fn view_only_rewind_without_a_stored_block_is_an_error() {
        use zcash_client_backend::data_api::{AccountBirthday, AccountPurpose, WalletWrite};
        use zcash_client_backend::keys::UnifiedSpendingKey;

        let dir = testdir::testdir!();
        let mut db = Db::init(dir.join("wallet.sqlite"), Network::TestNetwork).unwrap();
        let ufvk = UnifiedSpendingKey::from_seed(
            &Network::TestNetwork,
            &[0x11; 64],
            zip32::AccountId::ZERO,
        )
        .unwrap()
        .to_unified_full_viewing_key();
        let birthday =
            AccountBirthday::from_viewing_parts(BlockHeight::from_u32(1), BlockHash([0; 32]), None);
        db.data
            .import_account_ufvk("view", &ufvk, &birthday, AccountPurpose::ViewOnly, None)
            .unwrap();

        let error = rewind_for_continuity(&mut db, BlockHeight::from_u32(100)).unwrap_err();
        let message = format!("{error:?}");
        assert!(
            message.contains("no stored block"),
            "unexpected error: {message}"
        );
    }

    #[test]
    fn view_only_rewind_uses_the_newest_stored_block_below_the_error() {
        use zcash_client_backend::data_api::{AccountBirthday, AccountPurpose, WalletWrite};
        use zcash_client_backend::keys::UnifiedSpendingKey;

        let dir = testdir::testdir!();
        let mut db = Db::init(dir.join("wallet.sqlite"), Network::TestNetwork).unwrap();
        let ufvk = UnifiedSpendingKey::from_seed(
            &Network::TestNetwork,
            &[0x22; 64],
            zip32::AccountId::ZERO,
        )
        .unwrap()
        .to_unified_full_viewing_key();
        let birthday =
            AccountBirthday::from_viewing_parts(BlockHeight::from_u32(1), BlockHash([0; 32]), None);
        db.data
            .import_account_ufvk("view", &ufvk, &birthday, AccountPurpose::ViewOnly, None)
            .unwrap();
        let conn = Connection::open(&db.path).unwrap();
        conn.execute(
            "INSERT INTO blocks (
                height, hash, time,
                sapling_commitment_tree_size, sapling_output_count, sapling_tree,
                orchard_commitment_tree_size, orchard_action_count,
                ironwood_commitment_tree_size, ironwood_action_count
            ) VALUES (90, ?1, 0, 0, 0, x'00', 0, 0, 0, 0)",
            [vec![9u8; 32]],
        )
        .unwrap();

        let (height, hash) =
            view_only_rewind_target(&db, BlockHeight::from_u32(80), BlockHeight::from_u32(100))
                .unwrap()
                .expect("stored block 90 is below the error");
        assert_eq!(u32::from(height), 90);
        assert_eq!(hash, BlockHash([9; 32]));

        rewind_for_continuity(&mut db, BlockHeight::from_u32(100)).unwrap();
        let remaining: i64 = conn
            .query_row("SELECT COUNT(*) FROM blocks WHERE height > 90", [], |row| {
                row.get(0)
            })
            .unwrap();
        assert_eq!(remaining, 0);
    }

    #[test]
    fn view_only_prior_block_uses_compact_metadata() {
        use zcash_client_backend::proto::compact_formats::{
            ChainMetadata, CompactBlock, CompactOrchardAction, CompactSaplingOutput, CompactTx,
        };
        use zcash_primitives::block::BlockHash;

        let block = CompactBlock {
            height: 10,
            prev_hash: vec![7; 32],
            chain_metadata: Some(ChainMetadata {
                sapling_commitment_tree_size: 100,
                orchard_commitment_tree_size: 40,
                ironwood_commitment_tree_size: 5,
            }),
            vtx: vec![CompactTx {
                outputs: vec![
                    CompactSaplingOutput::default(),
                    CompactSaplingOutput::default(),
                ],
                actions: vec![CompactOrchardAction::default()],
                ironwood_actions: vec![
                    CompactOrchardAction::default(),
                    CompactOrchardAction::default(),
                ],
                ..Default::default()
            }],
            ..Default::default()
        };

        let prior = compact_chain_state_before(&block).unwrap();
        assert_eq!(u32::from(prior.block_height()), 9);
        assert_eq!(prior.block_hash(), BlockHash([7; 32]));
        assert_eq!(prior.sapling_tree_size(), 98);
        assert_eq!(prior.orchard_tree_size(), 39);
        assert_eq!(prior.ironwood_tree_size(), 3);
    }

    /// A short mainnet sync on `https://zec.rocks:443` must scan without subtree roots.
    ///
    /// The view-only wallet's shard tables stay empty. The spending wallet preloads subtree roots
    /// before it scans the same recent range.
    #[ignore]
    #[tokio_shared_rt::test(flavor = "multi_thread")]
    async fn view_only_near_tip_skips_subtree_roots() {
        use zcash_client_backend::data_api::{AccountPurpose, WalletRead};
        use zcash_client_backend::keys::UnifiedSpendingKey;

        let uri: http::Uri = "https://zec.rocks:443".parse().unwrap();
        let mut client = get_client(uri.clone()).await.unwrap();
        let info = client
            .get_lightd_info(service::Empty {})
            .await
            .unwrap()
            .into_inner();
        let network = parse_network(&info).unwrap();
        let tip = info.block_height;
        let birthday = tip.saturating_sub(20);
        eprintln!("near-tip tip={tip} birthday={birthday}");

        let dir = testdir::testdir!();
        let view_file = dir.join("view.sqlite");
        let mut view_db = Db::init(&view_file, network).unwrap();
        let ufvk = UnifiedSpendingKey::from_seed(&network, &[0x46; 64], zip32::AccountId::ZERO)
            .unwrap()
            .to_unified_full_viewing_key();
        view_db
            .import_account_ufvk(
                "view-only",
                &ufvk,
                AccountPurpose::ViewOnly,
                birthday,
                None,
                &mut client,
            )
            .await
            .unwrap();
        assert!(!view_db.data.maintains_note_commitment_trees().unwrap());
        sync(
            uri,
            &view_file,
            None,
            ConfirmationsPolicy::default(),
            false,
            CancellationToken::new(),
        )
        .await
        .unwrap();

        let conn = rusqlite::Connection::open(&view_file).unwrap();
        let blocks: i64 = conn
            .query_row("SELECT COUNT(*) FROM blocks", [], |row| row.get(0))
            .unwrap();
        let shards: i64 = conn
            .query_row("SELECT COUNT(*) FROM sapling_tree_shards", [], |row| {
                row.get(0)
            })
            .unwrap();
        assert!(blocks > 0, "view-only scan stored no blocks");
        assert_eq!(shards, 0, "view-only scan wrote sapling subtree roots");
    }

    /// Times a view-only UFVK wallet against a spending seed wallet.
    ///
    /// The birthday defaults to 3_487_990. Override it with `NERDBANK_ZCASH_PROFILE_BIRTHDAY`.
    /// The server is `https://zec.rocks:443`. `NERDBANK_ZCASH_SYNC_TIMING=1` prints phase totals;
    /// a view-only wallet should report no tree-state time.
    ///
    /// ```bash
    /// NERDBANK_ZCASH_SYNC_TIMING=1 \
    ///   cargo test -p nerdbank-zcash-rust --release --lib profile_view_only_and_spending_sync -- --ignored --nocapture --test-threads=1
    /// ```
    #[ignore]
    #[tokio_shared_rt::test(flavor = "multi_thread")]
    async fn profile_view_only_and_spending_sync() {
        use secrecy::SecretVec;
        use zcash_client_backend::data_api::{AccountPurpose, WalletRead};
        use zcash_client_backend::keys::UnifiedSpendingKey;

        let uri: http::Uri = "https://zec.rocks:443".parse().unwrap();
        let mut client = crate::grpc::get_client(uri.clone()).await.unwrap();
        let info = client
            .get_lightd_info(service::Empty {})
            .await
            .unwrap()
            .into_inner();
        let network = parse_network(&info).unwrap();
        let tip = info.block_height;
        let birthday = std::env::var("NERDBANK_ZCASH_PROFILE_BIRTHDAY")
            .ok()
            .and_then(|value| value.parse().ok())
            .unwrap_or(3_487_990);
        eprintln!(
            "profile tip={tip} birthday={birthday} span={}",
            tip.saturating_sub(birthday)
        );

        let view_dir = testdir::testdir!();
        let view_file = view_dir.join("view.sqlite");
        let mut view_db = Db::init(&view_file, network).unwrap();
        let view_seed = [0x44; 64];
        let view_ufvk = UnifiedSpendingKey::from_seed(&network, &view_seed, zip32::AccountId::ZERO)
            .unwrap()
            .to_unified_full_viewing_key();
        view_db
            .import_account_ufvk(
                "view-only",
                &view_ufvk,
                AccountPurpose::ViewOnly,
                birthday,
                None,
                &mut client,
            )
            .await
            .unwrap();
        assert!(!view_db.data.maintains_note_commitment_trees().unwrap());

        let view_started = std::time::Instant::now();
        let view_result = sync(
            uri.clone(),
            &view_file,
            None,
            ConfirmationsPolicy::default(),
            false,
            CancellationToken::new(),
        )
        .await
        .unwrap();
        let view_wall = view_started.elapsed();
        eprintln!(
            "view-only done wall={:.3}s tip={} fully_scanned={:?}",
            view_wall.as_secs_f64(),
            view_result.tip_height,
            view_result.last_fully_scanned_block
        );

        let spend_dir = testdir::testdir!();
        let spend_file = spend_dir.join("spend.sqlite");
        let mut spend_db = Db::init(&spend_file, network).unwrap();
        let spend_seed = SecretVec::new(vec![0x55; 64]);
        spend_db
            .add_account(
                "spending",
                &spend_seed,
                zip32::AccountId::ZERO,
                birthday,
                &mut client,
            )
            .await
            .unwrap();
        assert!(spend_db.data.maintains_note_commitment_trees().unwrap());

        let spend_started = std::time::Instant::now();
        let spend_result = sync(
            uri,
            &spend_file,
            None,
            ConfirmationsPolicy::default(),
            false,
            CancellationToken::new(),
        )
        .await
        .unwrap();
        let spend_wall = spend_started.elapsed();
        eprintln!(
            "spending done wall={:.3}s tip={} fully_scanned={:?}",
            spend_wall.as_secs_f64(),
            spend_result.tip_height,
            spend_result.last_fully_scanned_block
        );
    }

    const COIN: u64 = 1_0000_0000;

    fn format_zec(value: impl Into<ZatBalance>) -> String {
        let balance = value.into();
        let abs_value = i64::from(balance).unsigned_abs();
        let abs_zec = abs_value / COIN;
        let frac = abs_value % COIN;
        let zec = if balance.is_negative() {
            -(abs_zec as i64)
        } else {
            abs_zec as i64
        };
        format!("{zec:3}.{frac:08} ZEC")
    }
}
