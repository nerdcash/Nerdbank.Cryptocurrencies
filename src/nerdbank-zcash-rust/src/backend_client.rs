use std::path::Path;

use tokio::{fs::File, io::AsyncWriteExt};

use futures_util::TryStreamExt;
use http::Uri;
use prost::Message;
use tonic::transport::Channel;
use tracing::{debug, info};
use uniffi::deps::anyhow;
use zcash_client_sqlite::{chain::BlockMeta, error::SqliteClientError, FsBlockDbError, WalletDb};
use zcash_primitives::{
    consensus::{BlockHeight, Network, Parameters},
    merkle_tree::HashSer,
    sapling,
};

use zcash_client_backend::{
    data_api::{
        chain::{scan_cached_blocks, BlockSource, CommitmentTreeRoot},
        scanning::{ScanPriority, ScanRange},
        WalletCommitmentTrees, WalletRead, WalletWrite,
    },
    proto::service::compact_tx_streamer_client::CompactTxStreamerClient,
    proto::service::{self, LightdInfo},
};

use crate::{
    backing_store::{get_db, BlockCache, Db},
    error::Error,
    grpc::get_client,
    interop::SyncResult,
};

type ChainError =
    zcash_client_backend::data_api::chain::error::Error<SqliteClientError, FsBlockDbError>;

const BATCH_SIZE: u32 = 10_000;

fn parse_network(info: LightdInfo) -> Result<Network, Error> {
    match info.chain_name.as_str() {
        "main" => Ok(Network::MainNetwork),
        "test" => Ok(Network::TestNetwork),
        _ => Err(Error::InternalError(format!(
            "Unknown network: {}",
            info.chain_name
        ))),
    }
}

pub async fn sync<P: AsRef<Path>>(uri: Uri, wallet_dir: P) -> Result<SyncResult, Error> {
    let mut client = get_client(uri).await?;
    let info = client
        .get_lightd_info(service::Empty {})
        .await?
        .into_inner();
    let network = parse_network(info)?;

    let mut db = get_db(wallet_dir, network)?;

    // 1) Download note commitment tree data from lightwalletd
    // 2) Pass the commitment tree data to the database.
    update_subtree_roots(&mut client, &mut db.data).await?;

    loop {
        // 3) Download chain tip metadata from lightwalletd
        let tip_height: BlockHeight = client
            .get_latest_block(service::ChainSpec::default())
            .await?
            .get_ref()
            .height
            .try_into()
            .map_err(|e| Error::InternalError(format!("Invalid block height: {}", e)))?;

        // 4) Notify the wallet of the updated chain tip.
        db.data.update_chain_tip(tip_height)?;

        // 5) Get the suggested scan ranges from the wallet database
        let mut scan_ranges = db.data.suggest_scan_ranges()?;

        // 6) Run the following loop until the wallet's view of the chain tip as of the previous wallet
        //    session is valid.
        loop {
            // If there is a range of blocks that needs to be verified, it will always be returned as
            // the first element of the vector of suggested ranges.
            match scan_ranges.first() {
                Some(scan_range) if scan_range.priority() == ScanPriority::Verify => {
                    // Download the blocks in `scan_range` into the block source, overwriting any
                    // existing blocks in this range.
                    download_blocks(&mut client, &db.blocks, scan_range).await?;

                    // Scan the downloaded blocks and check for scanning errors that indicate that the wallet's chain tip
                    // is out of sync with blockchain history.
                    if scan_blocks(&network, &mut db, scan_range)? {
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
            // Download the blocks in `scan_range` into the block source.
            download_blocks(&mut client, &db.blocks, &scan_range).await?;

            // Scan the downloaded blocks.
            if scan_blocks(&network, &mut db, &scan_range)? {
                // The suggested scan ranges have been updated (either due to a continuity
                // error or because a higher priority range has been added).
                loop_around = true;
                break;
            }
        }

        if !loop_around {
            return Ok(SyncResult {
                tip_height: tip_height.into(),
            });
        }
    }
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

async fn download_blocks(
    client: &mut CompactTxStreamerClient<Channel>,
    block_cache: &BlockCache,
    scan_range: &ScanRange,
) -> Result<(), Error> {
    info!("Fetching {}", scan_range);
    let mut start = service::BlockId::default();
    start.height = scan_range.block_range().start.into();
    let mut end = service::BlockId::default();
    end.height = (scan_range.block_range().end - 1).into();
    let range = service::BlockRange {
        start: Some(start),
        end: Some(end),
    };
    let block_meta = client
        .get_block_range(range)
        .await
        .map_err(anyhow::Error::from)?
        .into_inner()
        .and_then(|block| async move {
            let (sapling_outputs_count, orchard_actions_count) = block
                .vtx
                .iter()
                .map(|tx| (tx.outputs.len() as u32, tx.actions.len() as u32))
                .fold((0, 0), |(acc_sapling, acc_orchard), (sapling, orchard)| {
                    (acc_sapling + sapling, acc_orchard + orchard)
                });

            let meta = BlockMeta {
                height: block.height(),
                block_hash: block.hash(),
                block_time: block.time,
                sapling_outputs_count,
                orchard_actions_count,
            };

            let encoded = block.encode_to_vec();
            let mut block_file = File::create(block_cache.block_path(&meta)).await?;
            block_file.write_all(&encoded).await?;

            Ok(meta)
        })
        .try_collect::<Vec<_>>()
        .await?;

    block_cache.blockmeta.write_block_metadata(&block_meta)?;

    Ok(())
}

/// Scans the given block range and checks for scanning errors that indicate the wallet's
/// chain tip is out of sync with blockchain history.
///
/// Returns `true` if scanning these blocks materially changed the suggested scan ranges.
fn scan_blocks(network: &Network, db: &mut Db, scan_range: &ScanRange) -> Result<bool, Error> {
    let scan_result = scan_cached_blocks(
        network,
        &mut db.blocks.blockmeta,
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
            db.blocks
                .blockmeta
                .with_blocks(Some(rewind_height + 1), None, |block| {
                    let meta = BlockMeta {
                        height: block.height(),
                        block_hash: block.hash(),
                        block_time: block.time,
                        // These values don't matter for deletion.
                        sapling_outputs_count: 0,
                        orchard_actions_count: 0,
                    };
                    std::fs::remove_file(db.blocks.block_path(&meta))
                        .map_err(|e| ChainError::BlockSource(FsBlockDbError::Fs(e)))
                })?;

            db.blocks.blockmeta.truncate_to_height(rewind_height)?;

            Ok(true)
        }
        Err(other) => Err(other.into()),
    }
}

#[cfg(test)]
mod tests {
    use crate::{backing_store::init, test_constants::TESTNET_LIGHTSERVER_ECC_URI};
    use testdir::testdir;

    use super::*;

    lazy_static! {
        static ref LIGHTSERVER_URI: Uri = TESTNET_LIGHTSERVER_ECC_URI.to_owned();
    }

    #[tokio::test]
    async fn test_sync() {
        let wallet_dir = testdir!();
        init(&wallet_dir, Network::TestNetwork).await.unwrap();
        let result = sync(LIGHTSERVER_URI.to_owned(), wallet_dir).await.unwrap();
        println!("result: {:?}", result);
    }
}
