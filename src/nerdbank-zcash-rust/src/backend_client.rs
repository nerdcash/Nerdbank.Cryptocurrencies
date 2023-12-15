use std::{
    cell::RefCell,
    collections::HashMap,
    fs,
    path::{Path, PathBuf},
    sync::Mutex,
};

use http::Uri;
use lazy_static::lazy_static;
use tonic::transport::{Channel, ClientTlsConfig};
use zcash_client_sqlite::{
    chain::init::init_blockmeta_db, wallet::init::init_wallet_db, FsBlockDb, WalletDb,
};
use zcash_primitives::{
    consensus::{BlockHeight, Network},
    sapling,
};

use zcash_client_backend::{
    data_api::{
        chain::{
            error::Error as BackendError, scan_cached_blocks, testing as chain_testing,
            BlockSource, CommitmentTreeRoot,
        },
        scanning::ScanPriority,
        testing, WalletCommitmentTrees, WalletRead, WalletWrite,
    },
    proto::service::compact_tx_streamer_client::CompactTxStreamerClient,
    proto::{compact_formats::ChainMetadata, service},
    scanning::{self, ScanError},
};

use crate::error::Error;

type MyError = Error<
    <testing::MockWalletDb as WalletRead>::Error,
    <chain_testing::MockBlockSource as BlockSource>::Error,
>;

const MY_SERVER: &str = "https://zcash.mysideoftheweb.com:9067";
const OFFICIAL_MAINNET: &str = "https://mainnet.lightwalletd.com:9067";

// We'll use a MUTEX to store global lightclient instances, by handle,
// so we don't have to keep creating it. We need to store it here, in rust
// because we can't return such a complex structure back to our client.
lazy_static! {
    static ref CHANNELS: Mutex<HashMap<Uri, RefCell<Channel>>> = Mutex::new(HashMap::new());
    static ref LIGHTSERVER_URI: Uri = Uri::from_static(MY_SERVER);
}

async fn get_grpc_channel(uri: Uri) -> Result<Channel, tonic::transport::Error> {
    let mut clients = CHANNELS.lock().unwrap();
    if let Some(client) = clients.get(&uri) {
        let channel = &*client.borrow();
        return Ok(channel.clone());
    }

    let tls = ClientTlsConfig::new().domain_name(uri.host().unwrap());
    let channel = Channel::builder(uri.clone())
        .tls_config(tls)?
        .connect()
        .await?;
    clients.insert(uri, RefCell::new(channel.clone()));
    Ok(channel)
}

fn destroy_channel(uri: Uri) -> bool {
    let mut clients = CHANNELS.lock().unwrap();
    clients.remove(&uri).is_some()
}

async fn get_client(uri: Uri) -> Result<CompactTxStreamerClient<Channel>, tonic::transport::Error> {
    let channel = get_grpc_channel(uri).await?;
    Ok(CompactTxStreamerClient::new(channel))
}

async fn get_block_height(uri: Uri) -> Result<u64, MyError> {
    let mut client = get_client(uri).await?;
    let response = client
        .get_lightd_info(service::Empty {})
        .await?
        .into_inner();
    Ok(response.block_height)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[tokio::test]
    async fn test_get_block_height() {
        let block_height = get_block_height(LIGHTSERVER_URI.to_owned()).await.unwrap();
        println!("block_height: {}", block_height);
    }

    #[tokio::test]
    async fn test_init() {
        let wallet_dir = Path::new("c:\\temp\\testwallet");
        init(LIGHTSERVER_URI.to_owned(), wallet_dir).await.unwrap();
    }
}

const DATA_DB: &str = "data.sqlite";

fn get_db_paths<P: AsRef<Path>>(wallet_dir: P, chain_name: &str) -> (PathBuf, PathBuf) {
    let mut a = wallet_dir.as_ref().to_owned();
    a.push(chain_name);

    let mut b = a.clone();
    b.push(DATA_DB);
    (a, b)
}

pub async fn init<P: AsRef<Path>>(uri: Uri, wallet_dir: P) -> Result<(), MyError> {
    let mut client = get_client(uri).await?;
    let response = client
        .get_lightd_info(service::Empty {})
        .await?
        .into_inner();
    let params = match response.chain_name.as_str() {
        "main" => Network::MainNetwork,
        "test" => Network::TestNetwork,
        _ => {
            return Err(MyError::InternalError(format!(
                "Invalid chain name: {}",
                response.chain_name
            )))
        }
    };

    let (db_cache, db_data) = get_db_paths(wallet_dir, response.chain_name.as_str());
    fs::create_dir_all(&db_cache)?;
    let mut db_cache = FsBlockDb::for_path(db_cache)?;
    let mut db_data = WalletDb::for_path(db_data, params)?;

    init_blockmeta_db(&mut db_cache)?;
    init_wallet_db(&mut db_data, None)?;

    Ok(())
}

pub async fn sync() -> Result<(), MyError> {
    let network = Network::TestNetwork;
    let block_source = chain_testing::MockBlockSource;
    let mut wallet_db = testing::MockWalletDb::new(Network::TestNetwork);

    // 1) Download note commitment tree data from lightwalletd
    let mut client = get_client(LIGHTSERVER_URI.to_owned()).await?;
    let tree_state = client.get_latest_tree_state(service::Empty {}).await?;
    let commitment_tree = tree_state.get_ref().sapling_tree();
    let roots: Vec<CommitmentTreeRoot<sapling::Node>> = unimplemented!();

    // 2) Pass the commitment tree data to the database.
    wallet_db.put_sapling_subtree_roots(0, &roots).unwrap();

    // 3) Download chain tip metadata from lightwalletd
    let tip_height: BlockHeight = unimplemented!();

    // 4) Notify the wallet of the updated chain tip.
    wallet_db
        .update_chain_tip(tip_height)
        .map_err(MyError::Wallet)?;

    // 5) Get the suggested scan ranges from the wallet database
    let mut scan_ranges = wallet_db.suggest_scan_ranges().map_err(MyError::Wallet)?;

    // 6) Run the following loop until the wallet's view of the chain tip as of the previous wallet
    //    session is valid.
    loop {
        // If there is a range of blocks that needs to be verified, it will always be returned as
        // the first element of the vector of suggested ranges.
        match scan_ranges.first() {
            Some(scan_range) if scan_range.priority() == ScanPriority::Verify => {
                // Download the blocks in `scan_range` into the block source, overwriting any
                // existing blocks in this range.
                unimplemented!();

                // Scan the downloaded blocks
                let scan_result = scan_cached_blocks(
                    &network,
                    &block_source,
                    &mut wallet_db,
                    scan_range.block_range().start,
                    scan_range.len(),
                );

                // Check for scanning errors that indicate that the wallet's chain tip is out of
                // sync with blockchain history.
                match scan_result {
                    Ok(_) => {
                        // At this point, the cache and scanned data are locally consistent (though
                        // not necessarily consistent with the latest chain tip - this would be
                        // discovered the next time this codepath is executed after new blocks are
                        // received) so we can break out of the loop.
                        break;
                    }
                    Err(BackendError::Scan(err)) if err.is_continuity_error() => {
                        // Pick a height to rewind to, which must be at least one block before
                        // the height at which the error occurred, but may be an earlier height
                        // determined based on heuristics such as the platform, available bandwidth,
                        // size of recent CompactBlocks, etc.
                        let rewind_height = err.at_height().saturating_sub(10);

                        // Rewind to the chosen height.
                        wallet_db
                            .truncate_to_height(rewind_height)
                            .map_err(MyError::Wallet)?;

                        // Delete cached blocks from rewind_height onwards.
                        //
                        // This does imply that assumed-valid blocks will be re-downloaded, but it
                        // is also possible that in the intervening time, a chain reorg has
                        // occurred that orphaned some of those blocks.
                        unimplemented!();
                    }
                    Err(other) => {
                        // Handle or return other errors
                    }
                }

                // In case we updated the suggested scan ranges, now re-request.
                scan_ranges = wallet_db.suggest_scan_ranges().map_err(MyError::Wallet)?;
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
    let scan_ranges = wallet_db.suggest_scan_ranges().map_err(MyError::Wallet)?;
    for scan_range in scan_ranges {
        // Download the blocks in `scan_range` into the block source. While in this example this
        // step is performed in-line, it's fine for the download of scan ranges to be asynchronous
        // and for the scanner to process the downloaded ranges as they become available in a
        // separate thread. The scan ranges should also be broken down into smaller chunks as
        // appropriate, and for ranges with priority `Historic` it can be useful to download and
        // scan the range in reverse order (to discover more recent unspent notes sooner), or from
        // the start and end of the range inwards.
        unimplemented!();

        // Scan the downloaded blocks.
        let scan_result = scan_cached_blocks(
            &network,
            &block_source,
            &mut wallet_db,
            scan_range.block_range().start,
            scan_range.len(),
        )?;

        // Handle scan errors, etc.
    }

    Ok(())
}
