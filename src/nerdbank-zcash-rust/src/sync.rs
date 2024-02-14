use std::{
    path::Path,
    sync::{Arc, Mutex},
};

use futures_util::TryStreamExt;
use http::Uri;
use prost::bytes::Buf;
use rusqlite::Connection;
use tonic::{transport::Channel, Status};
use tracing::{debug, info, warn};
use uniffi::deps::anyhow;
use zcash_client_sqlite::{error::SqliteClientError, WalletDb};
use zcash_primitives::{
    consensus::{BlockHeight, BranchId, Network, NetworkUpgrade, Parameters},
    legacy::TransparentAddress,
    merkle_tree::HashSer,
    transaction::{components::OutPoint, Transaction},
};

use zcash_client_backend::{
    data_api::{
        chain::{scan_cached_blocks, CommitmentTreeRoot},
        scanning::{ScanPriority, ScanRange},
        wallet::decrypt_and_store_transaction,
        WalletCommitmentTrees, WalletRead, WalletWrite,
    },
    encoding::AddressCodec,
    proto::service::{
        self, compact_tx_streamer_client::CompactTxStreamerClient, BlockId, BlockRange,
        TransparentAddressBlockFilter, TxFilter,
    },
    wallet::WalletTransparentOutput,
};

use crate::{
    backing_store::Db,
    block_source::{BlockCache, BlockCacheError},
    error::Error,
    grpc::get_client,
    interop::SyncResult,
    lightclient::parse_network,
    resilience::{webrequest_with_logged_retry, webrequest_with_retry},
};

type ChainError =
    zcash_client_backend::data_api::chain::error::Error<SqliteClientError, BlockCacheError>;

const BATCH_SIZE: u32 = 10_000;

pub async fn sync<P: AsRef<Path>>(uri: Uri, data_file: P) -> Result<SyncResult, Error> {
    let client = Arc::new(Mutex::new(get_client(uri).await?));
    let info = webrequest_with_retry(|| async {
        Ok(client
            .lock()
            .unwrap()
            .get_lightd_info(service::Empty {})
            .await?
            .into_inner())
    })
    .await?;
    let network = parse_network(&info)?;

    let mut db = Db::load(&data_file, network)?;

    // 1) Download note commitment tree data from lightwalletd
    // 2) Pass the commitment tree data to the database.
    update_subtree_roots(&mut client.lock().unwrap(), &mut db.data).await?;

    let mut tip_height: BlockHeight;
    loop {
        // 3) Download chain tip metadata from lightwalletd
        tip_height = webrequest_with_retry(|| async {
            Ok(client
                .lock()
                .unwrap()
                .get_latest_block(service::ChainSpec::default())
                .await?
                .get_ref()
                .height)
        })
        .await?
        .try_into()
        .map_err(|e| Error::InternalError(format!("Invalid block height: {}", e)))?;

        // 4) Notify the wallet of the updated chain tip.
        db.data.update_chain_tip(tip_height)?;

        // Download all the transparent ops related to the wallet first.
        // We don't need batches for this as that would just multiply the number of LWD requests we have to make.
        for (address, height) in db.data.get_transparent_addresses_and_sync_heights()? {
            download_transparent_transactions(
                &mut client.lock().unwrap(),
                &mut db,
                &network,
                &address,
                height,
                tip_height,
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
                        &mut client.lock().unwrap(),
                        scan_range,
                        &network,
                        &mut db,
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
        let mut loop_around = false;
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
            if download_and_scan_blocks(&mut client.lock().unwrap(), &scan_range, &network, &mut db)
                .await?
            {
                // The suggested scan ranges have been updated (either due to a continuity
                // error or because a higher priority range has been added).
                loop_around = true;
                break;
            }
        }

        if !loop_around {
            break;
        }
    }

    // Download and decrypt the full transactions we found in the compact blocks
    // so we can save their memos to the database.
    download_full_transactions(&mut client.lock().unwrap(), data_file, &mut db, &network).await?;

    Ok(SyncResult {
        latest_block: tip_height.into(),
    })
}

async fn download_full_transactions<P: AsRef<Path>>(
    client: &mut CompactTxStreamerClient<Channel>,
    data_file: P,
    db: &mut Db,
    network: &Network,
) -> Result<(), Error> {
    let client = Arc::new(Mutex::new(client));
    // Scope the database connection so it's closed before we use the db argument,
    // to avoid 'database is locked' errors.
    let txids;
    {
        let conn = Connection::open(data_file)?;
        let mut stmt = conn.prepare("SELECT txid FROM transactions WHERE raw IS NULL")?;
        txids = stmt
            .query_map([], |r| r.get::<_, Vec<u8>>(0))?
            .collect::<Result<Vec<_>, _>>()?;
    }

    for txid in txids {
        let raw_tx = webrequest_with_retry(|| async {
            Ok(client
                .lock()
                .unwrap()
                .get_transaction(TxFilter {
                    hash: txid.clone(),
                    ..Default::default()
                })
                .await?
                .into_inner())
        })
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

    Ok(())
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
) -> Result<(), Error> {
    let client = Arc::new(Mutex::new(client));
    let transparent_transactions = webrequest_with_retry(|| async {
        Ok(client
            .lock()
            .unwrap()
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
            .await?)
    })
    .await?;
    for rawtx in transparent_transactions {
        let height = BlockHeight::from_u32(rawtx.height as u32);
        let tx = Transaction::read(&rawtx.data[..], BranchId::for_height(network, height))?;
        if let Some(t) = tx.transparent_bundle() {
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
) -> Result<bool, Error> {
    // Download the blocks in `scan_range` into the block source, overwriting any
    // existing blocks in this range.
    let client = Arc::new(Mutex::new(client));
    let db = Arc::new(Mutex::new(db));
    webrequest_with_logged_retry(
        || async {
            let mut client = client.lock().unwrap();
            let mut db = db.lock().unwrap();
            download_blocks(&mut client, &mut db.blocks, scan_range).await
        },
        |error, duration, failure_count| {
            let msg = format!(
                "Failure {} to download blocks: {}. {}. Will retry in {:?}.",
                failure_count, scan_range, error, duration
            );
            warn!("{}", &msg);
            println!("{}", &msg);
        },
    )
    .await?;

    // Scan the downloaded blocks.
    let result = scan_blocks(network, &mut db.lock().unwrap(), scan_range)?;

    // Now that they've been scanned, we don't need them any more.
    db.lock()
        .unwrap()
        .blocks
        .remove_range(scan_range.block_range());

    Ok(result)
}

async fn download_blocks(
    client: &mut CompactTxStreamerClient<Channel>,
    block_cache: &mut BlockCache,
    scan_range: &ScanRange,
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
    let compact_blocks = client
        .get_block_range(range)
        .await?
        .into_inner()
        .try_collect::<Vec<_>>()
        .await?;

    block_cache.insert_range(compact_blocks);

    Ok(())
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

        let result = sync(setup.server_uri.clone(), &setup.data_file)
            .await
            .unwrap();

        println!("Tip: {:?}", result.latest_block);

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
