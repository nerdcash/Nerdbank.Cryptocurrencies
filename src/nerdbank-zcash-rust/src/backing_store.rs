use std::{fs, path::Path};

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

pub(crate) struct Db {
    pub(crate) data: WalletDb<Connection, Network>,
    pub(crate) blocks: BlockCache,
}

impl Db {
    /// Initializes the database for the given wallet, creating it if it does not exist,
    /// or upgrading its schema if it already exists and is out of date.
    /// This should be used the first time a wallet is opened in each session (or at least the first time ever, and once after each software upgrade).
    pub(crate) fn init<P: AsRef<Path>>(data_file: P, network: Network) -> Result<Db, Error> {
        get_db_internal(data_file, network, true)
    }

    /// Opens the database for the given wallet. An error will result if it does not already exist.
    pub(crate) fn load<P: AsRef<Path>>(data_file: P, network: Network) -> Result<Db, Error> {
        get_db_internal(data_file, network, false)
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
    data_file: P,
    network: Network,
    init: bool,
) -> Result<Db, Error> {
    if init {
        if let Some(data_dir) = data_file.as_ref().to_owned().parent() {
            fs::create_dir_all(&data_dir)?;
        }
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

#[cfg(test)]
mod tests {
    use super::*;
    use testdir::testdir;

    #[tokio::test]
    async fn test_init() {
        let wallet_dir = testdir!();
        let data_file = wallet_dir.join("wallet.sqlite");
        Db::init(data_file, Network::TestNetwork).unwrap();
    }
}
