use std::{
    fs,
    path::{Path, PathBuf},
};

use rusqlite::Connection;
use secrecy::SecretVec;
use tonic::transport::Channel;
use zcash_client_backend::{
    data_api::{AccountBirthday, WalletWrite},
    keys::UnifiedSpendingKey,
    proto::service::{self, compact_tx_streamer_client::CompactTxStreamerClient},
};
use zcash_client_sqlite::{wallet::init::init_wallet_db, WalletDb};
use zcash_primitives::{consensus::Network, zip32::AccountId};

use crate::{block_source::BlockCache, error::Error};

const DATA_DB: &str = "data.sqlite";

pub(crate) struct Db {
    pub(crate) data: WalletDb<Connection, Network>,
    pub(crate) blocks: BlockCache,
}

impl Db {
    /// Initializes the database for the given wallet, creating it if it does not exist,
    /// or upgrading its schema if it already exists and is out of date.
    /// This should be used the first time a wallet is opened in each session (or at least the first time ever, and once after each software upgrade).
    pub(crate) async fn init<P: AsRef<Path>>(wallet_dir: P, network: Network) -> Result<Db, Error> {
        get_db_internal(wallet_dir, network, true)
    }

    /// Opens the database for the given wallet. An error will result if it does not already exist.
    pub(crate) fn load<P: AsRef<Path>>(wallet_dir: P, network: Network) -> Result<Db, Error> {
        get_db_internal(wallet_dir, network, false)
    }

    pub(crate) async fn add_account(
        &mut self,
        seed: &SecretVec<u8>,
        birthday: u64,
        client: &mut CompactTxStreamerClient<Channel>,
    ) -> Result<(AccountId, UnifiedSpendingKey), Error> {
        // Construct an `AccountBirthday` for the account's birthday.
        let birthday = {
            // Fetch the tree state corresponding to the last block prior to the wallet's
            // birthday height. NOTE: THIS APPROACH LEAKS THE BIRTHDAY TO THE SERVER!
            let request = service::BlockId {
                height: birthday - 1,
                ..Default::default()
            };
            let treestate = client.get_tree_state(request).await?.into_inner();
            AccountBirthday::from_treestate(treestate, None)?
        };

        Ok(self.data.create_account(seed, birthday)?)
    }
}

fn get_db_internal<P: AsRef<Path>>(
    wallet_dir: P,
    network: Network,
    init: bool,
) -> Result<Db, Error> {
    let (data_dir, data_file) = get_db_paths(wallet_dir, network);
    if init {
        fs::create_dir_all(&data_dir)?;
    }
    let mut data = WalletDb::for_path(data_file, network)?;

    if init {
        init_wallet_db(&mut data, None)?;
    }

    Ok(Db {
        data,
        blocks: BlockCache::new(),
    })
}

/// Gets the path to the directory containing the db file, and the path to the db file itself.
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
        Db::init(wallet_dir, Network::TestNetwork).await.unwrap();
    }
}
