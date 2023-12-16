use std::{
    fs,
    path::{Path, PathBuf},
};

use rusqlite::Connection;
use zcash_client_sqlite::{
    chain::{init::init_blockmeta_db, BlockMeta},
    wallet::init::init_wallet_db,
    FsBlockDb, WalletDb,
};
use zcash_primitives::consensus::Network;

use crate::error::Error;

const DATA_DB: &str = "data.sqlite";
const BLOCKS_FOLDER: &str = "blocks"; // This must match what zcash_client_sqlite

pub(crate) struct Db {
    pub(crate) data: WalletDb<Connection, Network>,
    pub(crate) blocks: BlockCache,
}

pub(crate) struct BlockCache {
    pub(crate) blockmeta: FsBlockDb,
	/// The path to the directory containing compact blocks.
    path: PathBuf,
}

impl BlockCache {
    /// Gets the path to the file that contains an individual block.
    pub(crate) fn block_path(&self, meta: &BlockMeta) -> PathBuf {
        meta.block_file_path(&self.path)
    }
}

/// Initializes the database for the given wallet, creating it if it does not exist,
/// or upgrading its schema if it already exists and is out of date.
/// This should be used the first time a wallet is opened in each session (or at least the first time ever, and once after each software upgrade).
pub(crate) async fn init_db<P: AsRef<Path>>(wallet_dir: P, network: Network) -> Result<Db, Error> {
    get_db_internal(wallet_dir, network, true)
}

/// Opens the database for the given wallet. An error will result if it does not already exist.
pub(crate) fn get_db<P: AsRef<Path>>(wallet_dir: P, network: Network) -> Result<Db, Error> {
    get_db_internal(wallet_dir, network, false)
}

fn get_db_internal<P: AsRef<Path>>(
    wallet_dir: P,
    network: Network,
    init: bool,
) -> Result<Db, Error> {
    let (cache_path, data_path) = get_db_paths(wallet_dir, network);
    if init {
        fs::create_dir_all(&cache_path)?;
    }
    let mut blocks = FsBlockDb::for_path(&cache_path)?;
    let mut data = WalletDb::for_path(data_path, network)?;

    if init {
        init_blockmeta_db(&mut blocks)?;
        init_wallet_db(&mut data, None)?;
    }

    Ok(Db {
        data,
        blocks: BlockCache {
            blockmeta: blocks,
            path: cache_path.join(BLOCKS_FOLDER),
        },
    })
}

fn get_db_paths<P: AsRef<Path>>(wallet_dir: P, network: Network) -> (PathBuf, PathBuf) {
    let mut a = wallet_dir.as_ref().to_owned();
    a.push(match network {
        Network::MainNetwork => "mainnet",
        Network::TestNetwork => "testnet",
    });

    let mut b = a.clone();
    b.push(DATA_DB);
    (a, b)
}

#[cfg(test)]
mod tests {
    use super::*;
    use testdir::testdir;

    #[tokio::test]
    async fn test_init() {
        let wallet_dir = testdir!();
        init_db(wallet_dir, Network::TestNetwork).await.unwrap();
    }
}
