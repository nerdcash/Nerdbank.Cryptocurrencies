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
const BLOCKS_FOLDER: &str = "blocks";

pub async fn init<P: AsRef<Path>>(wallet_dir: P, network: Network) -> Result<(), Error> {
    let (db_cache, db_data) = get_db_paths(wallet_dir, network);
    fs::create_dir_all(&db_cache)?;
    let mut db_cache = FsBlockDb::for_path(db_cache)?;
    let mut db_data = WalletDb::for_path(db_data, network)?;

    init_blockmeta_db(&mut db_cache)?;
    init_wallet_db(&mut db_data, None)?;

    Ok(())
}

pub(crate) fn get_db_paths<P: AsRef<Path>>(wallet_dir: P, network: Network) -> (PathBuf, PathBuf) {
    let mut a = wallet_dir.as_ref().to_owned();
    a.push(match network {
        Network::MainNetwork => "mainnet",
        Network::TestNetwork => "testnet",
    });

    let mut b = a.clone();
    b.push(DATA_DB);
    (a, b)
}

pub(crate) struct Db {
    pub(crate) data: WalletDb<Connection, Network>,
    pub(crate) blocks: BlockCache,
}

pub(crate) struct BlockCache {
    pub(crate) blockmeta: FsBlockDb,
    pub(crate) cache_path: PathBuf,
}

impl BlockCache {
    /// Gets the path to the file that contains an individual block.
    pub(crate) fn block_path(&self, meta: &BlockMeta) -> PathBuf {
        meta.block_file_path(&self.cache_path.join(BLOCKS_FOLDER))
    }
}

pub(crate) fn get_db<P: AsRef<Path>>(wallet_dir: P, network: Network) -> Result<Db, Error> {
    let (cache_path, data_path) = get_db_paths(wallet_dir, network);
    let cache_path = cache_path.as_path().to_owned();
    let blocks = FsBlockDb::for_path(&cache_path)?;
    let data = WalletDb::for_path(data_path, network)?;
    Ok(Db {
        data,
        blocks: BlockCache {
            blockmeta: blocks,
            cache_path: cache_path,
        },
    })
}

#[cfg(test)]
mod tests {
    use super::*;
    use testdir::testdir;

    #[tokio::test]
    async fn test_init() {
        let wallet_dir = testdir!();
        init(wallet_dir, Network::TestNetwork).await.unwrap();
    }
}
