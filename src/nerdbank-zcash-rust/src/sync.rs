use futures_util::TryStreamExt;
use http::Uri;
use orchard::keys::Scope;
use prost::bytes::Buf;
use rusqlite::{named_params, Connection};
use std::{path::Path, sync::Arc};
use tokio::{select, sync::Mutex};
use tokio_util::sync::CancellationToken;
use tonic::{transport::Channel, Status};
use tracing::{debug, info, warn};
use uniffi::deps::anyhow;
use zcash_client_sqlite::{error::SqliteClientError, WalletDb};
use zcash_keys::address::UnifiedAddress;
use zcash_primitives::{
    consensus::{BlockHeight, BranchId, Network, NetworkUpgrade, Parameters},
    legacy::TransparentAddress,
    merkle_tree::HashSer,
    transaction::{components::OutPoint, Transaction},
    zip32::AccountId,
};

use zcash_client_backend::{
    data_api::{
        chain::{scan_cached_blocks, CommitmentTreeRoot},
        scanning::{ScanPriority, ScanRange},
        wallet::decrypt_and_store_transaction,
        WalletCommitmentTrees, WalletRead, WalletWrite,
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
    interop::{ShieldedNote, SyncUpdate, SyncUpdateData, TransactionSendDetail, TransparentNote},
    lightclient::parse_network,
    resilience::{webrequest_with_logged_retry, webrequest_with_retry},
    sql_statements::GET_TRANSACTIONS_SQL,
};

type ChainError =
    zcash_client_backend::data_api::chain::error::Error<SqliteClientError, BlockCacheError>;

const BATCH_SIZE: u32 = 10_000;

pub async fn sync<P: AsRef<Path>>(
    uri: Uri,
    data_file: P,
    progress: Option<Box<dyn SyncUpdate>>,
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

    // 1) Download note commitment tree data from lightwalletd
    // 2) Pass the commitment tree data to the database.
    update_subtree_roots(&mut client.clone(), &mut db.data).await?;

    let mut tip_height: BlockHeight;
    loop {
        // 3) Download chain tip metadata from lightwalletd
        tip_height = webrequest_with_retry(
            || async {
                Ok(client
                    .clone()
                    .get_latest_block(service::ChainSpec::default())
                    .await?
                    .get_ref()
                    .height)
            },
            cancellation_token.clone(),
        )
        .await?
        .try_into()
        .map_err(|e| Error::Internal(format!("Invalid block height: {}", e)))?;

        // 4) Notify the wallet of the updated chain tip.
        db.data.update_chain_tip(tip_height)?;

        // Download all the transparent ops related to the wallet first.
        // We don't need batches for this as that would just multiply the number of LWD requests we have to make.
        for (address, height) in db.data.get_transparent_addresses_and_sync_heights()? {
            download_transparent_transactions(
                &mut client,
                &mut db,
                &network,
                &address,
                height,
                tip_height,
                cancellation_token.clone(),
            )
            .await?;
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
                    if download_and_scan_blocks(
                        &mut client,
                        scan_range,
                        &network,
                        &mut db,
                        cancellation_token.clone(),
                    )
                    .await?
                    {
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

        let mut status = SyncUpdateData {
            current_step: 0,
            // Establish the total work required.
            // For now, we'll just count blocks. But a far better measure is by number of outputs we'll need to scan.
            total_steps: scan_ranges.iter().map(|r| r.len()).sum::<usize>() as u64,
            last_fully_scanned_block: None,
            tip_height: u32::from(tip_height),
            last_error: None,
        };

        fn update_status<'a>(
            status: &'a mut SyncUpdateData,
            data: &WalletDb<Connection, Network>,
        ) -> Result<&'a SyncUpdateData, Error> {
            status.last_fully_scanned_block =
                data.block_fully_scanned()?.map(|b| b.block_height().into());
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
            progress: &Option<Box<dyn SyncUpdate>>,
        ) -> Result<(), Error> {
            report_status(update_status(status, data)?, progress);
            Ok(())
        }

        update_and_report_status(&mut status, &db.data, &progress)?;

        let mut caught_up = true;
        for scan_range in scan_ranges.into_iter().flat_map(|r| {
            // Limit the number of blocks we download and scan at any one time.
            (0..).scan(r, |acc, _| {
                if acc.is_empty() {
                    None
                } else if let Some((cur, next)) = acc.split_at(acc.block_range().start + BATCH_SIZE)
                {
                    *acc = next;
                    Some(cur)
                } else {
                    let cur = acc.clone();
                    let end = acc.block_range().end;
                    *acc = ScanRange::from_parts(end..end, acc.priority());
                    Some(cur)
                }
            })
        }) {
            if download_and_scan_blocks(
                &mut client,
                &scan_range,
                &network,
                &mut db,
                cancellation_token.clone(),
            )
            .await?
            {
                // The suggested scan ranges have been updated (either due to a continuity
                // error or because a higher priority range has been added).
                caught_up = false;
                break;
            }

            // Download and decrypt the full transactions we found in the compact blocks
            // so we can save their memos to the database.
            let new_transactions = download_full_transactions(
                &mut client,
                &data_file,
                &mut db,
                &network,
                cancellation_token.clone(),
            )
            .await?;

            if !new_transactions.is_empty() {
                if let Some(sink) = progress.as_ref() {
                    sink.report_transactions(new_transactions);
                }
            }

            status.current_step += scan_range.len() as u64;
            update_and_report_status(&mut status, &db.data, &progress)?;
        }

        if caught_up {
            update_status(&mut status, &db.data)?;

            if !continually {
                return Ok(status);
            }

            report_status(&status, &progress);

            // We'll loop around again when the next block is mined.
            // Eventually we should actually do something with the transactions in the mempool too.
            select! {
                _ = cancellation_token.cancelled() => Err(Status::cancelled("Request cancelled")),
                _ = watch_mempool(&mut client) => Ok(()),
            }?;
        }
    }
}

async fn download_full_transactions<P: AsRef<Path> + Clone>(
    client: &mut CompactTxStreamerClient<Channel>,
    data_file: P,
    db: &mut Db,
    network: &Network,
    cancellation_token: CancellationToken,
) -> Result<Vec<crate::interop::Transaction>, Error> {
    let client = Arc::new(Mutex::new(client));
    // Scope the database connection so it's closed before we use the db argument,
    // to avoid 'database is locked' errors.
    let txids;
    {
        let conn = Connection::open(data_file.clone())?;
        let mut stmt = conn.prepare("SELECT txid FROM transactions WHERE raw IS NULL")?;
        txids = stmt
            .query_map([], |r| r.get::<_, Vec<u8>>(0))?
            .collect::<Result<Vec<_>, _>>()?;
    }

    for txid in txids.iter() {
        let raw_tx = webrequest_with_retry(
            || async {
                Ok(client
                    .lock()
                    .await
                    .get_transaction(TxFilter {
                        hash: txid.clone(),
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

    let mut conn = Connection::open(data_file)?;
    Ok(get_transactions(db, &mut conn, network, None, None)?
        .iter()
        .filter(|r| txids.contains(&r.txid))
        .cloned()
        .collect::<Vec<_>>())
}

async fn update_subtree_roots<P: Parameters>(
    client: &mut CompactTxStreamerClient<Channel>,
    db_data: &mut WalletDb<rusqlite::Connection, P>,
) -> Result<(), anyhow::Error> {
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
) -> Result<(), Error> {
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
        if let Some(t) = tx.transparent_bundle() {
            // TODO: record UTXOs SPENDING too
            for (txout_index, txout) in t.vout.iter().enumerate() {
                let outpoint = OutPoint::new(tx.txid().as_ref().to_owned(), txout_index as u32);
                if let Some(output) =
                    WalletTransparentOutput::from_parts(outpoint, txout.to_owned(), height)
                {
                    db.data.put_received_transparent_utxo(&output)?;
                }
            }
        }
    }

    db.data
        .put_latest_scanned_block_for_transparent(address, BlockHeight::from_u32(end.into()))?;

    Ok(())
}

async fn download_and_scan_blocks(
    client: &mut CompactTxStreamerClient<Channel>,
    scan_range: &ScanRange,
    network: &Network,
    db: &mut Db,
    cancellation_token: CancellationToken,
) -> Result<bool, Error> {
    // Download the blocks in `scan_range` into the block source, overwriting any
    // existing blocks in this range.
    let client = Arc::new(Mutex::new(client));
    db.blocks.insert_range(
        webrequest_with_logged_retry(
            || async {
                let mut client = client.lock().await;
                download_blocks(&mut client, scan_range).await
            },
            |error, duration, failure_count| {
                let msg = format!(
                    "Failure {} to download blocks: {}. {}. Will retry in {:?}.",
                    failure_count, scan_range, error, duration
                );
                warn!("{}", &msg);
                println!("{}", &msg);
            },
            cancellation_token,
        )
        .await?,
    );

    // Scan the downloaded blocks.
    let result = scan_blocks(network, db, scan_range)?;

    // Now that they've been scanned, we don't need them any more.
    db.blocks.remove_range(scan_range.block_range());

    Ok(result)
}

async fn download_blocks(
    client: &mut CompactTxStreamerClient<Channel>,
    scan_range: &ScanRange,
) -> Result<Vec<CompactBlock>, Status> {
    info!("Fetching {}", scan_range);
    let mut start = service::BlockId::default();
    start.height = scan_range.block_range().start.into();
    let mut end = service::BlockId::default();
    end.height = (scan_range.block_range().end - 1).into();
    let range = service::BlockRange {
        start: Some(start),
        end: Some(end),
    };
    client
        .get_block_range(range)
        .await?
        .into_inner()
        .try_collect::<Vec<_>>()
        .await
}

/// Scans the given block range and checks for scanning errors that indicate the wallet's
/// chain tip is out of sync with blockchain history.
///
/// Returns `true` if scanning these blocks materially changed the suggested scan ranges.
fn scan_blocks(network: &Network, db: &mut Db, scan_range: &ScanRange) -> Result<bool, Error> {
    let scan_result = scan_cached_blocks(
        network,
        &db.blocks,
        &mut db.data,
        scan_range.block_range().start,
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

pub fn get_transactions(
    db: &mut Db,
    conn: &mut rusqlite::Connection,
    network: &Network,
    account_id_filter: Option<u32>,
    starting_block_filter: Option<u32>,
) -> Result<Vec<crate::interop::Transaction>, Error> {
    let ufvkeys = db.data.get_unified_full_viewing_keys()?;

    rusqlite::vtab::array::load_module(conn)?;

    let mut stmt_txs = conn.prepare(GET_TRANSACTIONS_SQL)?;

    let rows = stmt_txs.query_and_then(
        named_params! {
            ":account_id": account_id_filter,
            ":starting_block": starting_block_filter,
        },
        |row| -> Result<crate::interop::Transaction, Error> {
            let account_id: u32 = row.get("account_id")?;
            let output_pool: u32 = row.get("output_pool")?;
            let from_account: Option<u32> = row.get("from_account")?;
            let to_account: Option<u32> = row.get("to_account")?;
            let mut recipient: Option<String> = row.get("to_address")?;
            let value: u64 = row.get("value")?;
            let memo: Option<Vec<u8>> = row.get("memo")?;
            let memo = memo.unwrap_or_default();

            let ufvk = ufvkeys.get(
                &AccountId::try_from(account_id)
                    .map_err(|_| Error::InvalidArgument("Invalid account ID".to_string()))?,
            );

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
                fee: row.get::<_, Option<u64>>("fee_paid")?.unwrap_or(0),
                account_balance_delta: row.get("account_balance_delta")?,
                incoming_transparent: Vec::new(),
                incoming_shielded: Vec::new(),
                outgoing: Vec::new(),
            };

            if to_account == Some(account_id) {
                match output_pool {
                    0 => tx.incoming_transparent.push(TransparentNote {
                        value,
                        recipient: recipient.clone().unwrap(),
                    }),
                    1..=3 => tx.incoming_shielded.push(ShieldedNote {
                        value,
                        memo: memo.clone(),
                        recipient: recipient.clone().unwrap(), // TODO: this will fail because recipient is NULL. Reconstruct from diversifier.
                        is_change: false,
                    }),
                    _ => {
                        return Err(Error::SqliteClient(SqliteClientError::CorruptedData(
                            format!("Unsupported output pool value {}.", output_pool),
                        )));
                    }
                }
            };

            if let Some(recipient) = recipient {
                if from_account == Some(account_id) {
                    tx.outgoing.push(TransactionSendDetail {
                        recipient,
                        memo: Some(memo),
                        value,
                    });
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
            tx.incoming_transparent
                .append(&mut row.incoming_transparent);
            tx.incoming_shielded.append(&mut row.incoming_shielded);
            tx.outgoing.append(&mut row.outgoing);
            result.push(tx);
        } else {
            result.push(row);
        }
    }

    Ok(result)
}

#[cfg(test)]
mod tests {
    use zcash_primitives::transaction::components::Amount;

    use crate::test_constants::{create_account, setup_test, MIN_CONFIRMATIONS};

    use super::*;

    #[tokio::test]
    async fn test_sync() {
        let mut setup = setup_test().await;
        let (seed, birthday, account_id, _) = create_account(&mut setup).await.unwrap();

        // Add one more account two accounts with the same seed leads to accounts with unique indexes and thus spending authorities.
        let account_ids = [
            account_id,
            setup
                .db
                .add_account(&seed, birthday, &mut setup.client)
                .await
                .unwrap()
                .0,
        ]
        .map(|a| a);

        let result = sync(
            setup.server_uri.clone(),
            &setup.data_file,
            None,
            false,
            CancellationToken::new(),
        )
        .await
        .unwrap();

        println!("Tip: {:?}", result.last_fully_scanned_block);

        if let Some(summary) = setup.db.data.get_wallet_summary(MIN_CONFIRMATIONS).unwrap() {
            for id in account_ids {
                println!("Account index: {}", u32::from(id));
                let b = summary.account_balances().get(&id).unwrap();
                println!(
                    "Sapling balance: {}",
                    format_zec(Amount::from(b.sapling_balance().spendable_value()))
                );
                println!("Transparent balance: {}", format_zec(b.unshielded()));
            }
        } else {
            println!("No summary found");
        }
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
