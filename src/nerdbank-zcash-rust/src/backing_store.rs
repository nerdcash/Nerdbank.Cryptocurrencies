use std::{fs, path::Path};

use rand::rngs::OsRng;
use rusqlite::Connection;
use secrecy::SecretVec;
use tonic::transport::Channel;
use zcash_client_backend::{
    data_api::{AccountBirthday, AccountPurpose, WalletWrite},
    keys::UnifiedSpendingKey,
    proto::service::{self, compact_tx_streamer_client::CompactTxStreamerClient},
};
use zcash_client_sqlite::{
    AccountUuid, WalletDb, util::SystemClock, wallet::{Account, init::init_wallet_db}
};
use zcash_keys::{address::UnifiedAddress, keys::UnifiedFullViewingKey};
use zcash_protocol::consensus::Network;
use zip32::DiversifierIndex;

use crate::{block_source::BlockCache, error::Error};

pub(crate) struct Db {
    pub(crate) data: WalletDb<Connection, Network, SystemClock, OsRng>,
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
        name: &str,
        seed: &SecretVec<u8>,
        account_index: zip32::AccountId,
        birthday: u64,
        client: &mut CompactTxStreamerClient<Channel>,
    ) -> Result<(Account, UnifiedSpendingKey), Error> {
        // Get the current chain height (for the wallet's birthday and/or recover-until height).
        let chain_tip: u32 = client
            .get_latest_block(service::ChainSpec::default())
            .await?
            .into_inner()
            .height
            .try_into()
            .expect("block heights must fit into u32");
        let recover_until = Some(chain_tip.into());

        // Construct an `AccountBirthday` for the account's birthday.
        let birthday = {
            // Fetch the tree state corresponding to the last block prior to the wallet's
            // birthday height. NOTE: THIS APPROACH LEAKS THE BIRTHDAY TO THE SERVER!
            let request = service::BlockId {
                height: birthday.saturating_sub(1),
                ..Default::default()
            };
            let treestate = client.get_tree_state(request).await?.into_inner();
            AccountBirthday::from_treestate(treestate, recover_until)?
        };

        Ok(self
            .data
            .import_account_hd(name, seed, account_index, &birthday, None)?)
    }

    pub(crate) async fn import_account_ufvk(
        &mut self,
        name: &str,
        ufvk: &UnifiedFullViewingKey,
        purpose: AccountPurpose,
        birthday: u64,
        key_source: Option<&str>,
        client: &mut CompactTxStreamerClient<Channel>,
    ) -> Result<Account, Error> {
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

        Ok(self
            .data
            .import_account_ufvk(name, ufvk, &birthday, purpose, key_source)?)
    }

    pub(crate) fn add_diversifier(
        &mut self,
        account_id: AccountUuid,
        diversifier_index: DiversifierIndex,
    ) -> Result<UnifiedAddress, Error> {
        Ok(self
            .data
            .put_address_with_diversifier_index(account_id, diversifier_index)?)
    }
}

fn get_db_internal<P: AsRef<Path>>(
    data_file: P,
    network: Network,
    init: bool,
) -> Result<Db, Error> {
    if init {
        if let Some(data_dir) = data_file.as_ref().to_owned().parent() {
            fs::create_dir_all(data_dir)?;
        }
    }

    let mut data = WalletDb::for_path(data_file, network, SystemClock, OsRng)?;

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

    #[tokio_shared_rt::test]
    async fn test_init() {
        let wallet_dir = testdir!();
        let data_file = wallet_dir.join("wallet.sqlite");
        Db::init(data_file, Network::TestNetwork).unwrap();
    }
}
