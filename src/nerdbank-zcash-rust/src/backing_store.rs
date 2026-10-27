use std::{fs, path::Path};

use rand::{rand_core::UnwrapErr, rngs::SysRng};
use rusqlite::Connection;
use secrecy::SecretVec;
use tonic::transport::Channel;
use zcash_client_backend::{
    data_api::{AccountBirthday, AccountPurpose, WalletWrite},
    keys::UnifiedSpendingKey,
    proto::service::{self, compact_tx_streamer_client::CompactTxStreamerClient},
};
use zcash_client_sqlite::{
    AccountUuid, WalletDb,
    util::SystemClock,
    wallet::{Account, init::init_wallet_db},
};
use zcash_keys::{
    address::UnifiedAddress,
    keys::{UnifiedFullViewingKey, UnifiedIncomingViewingKey},
};
use zcash_protocol::consensus::Network;
use zip32::DiversifierIndex;

use crate::{block_source::BlockCache, error::Error};

/// Operating-system RNG stored on [`WalletDb`].
///
/// `rand` 0.10's [`SysRng`] reports operating-system failures as errors.
/// [`WalletDb`] requires [`rand::Rng`], so this wrapper panics if the operating
/// system cannot provide entropy.
pub(crate) type WalletRng = UnwrapErr<SysRng>;

pub(crate) struct Db {
    pub(crate) data: WalletDb<Connection, Network, SystemClock, WalletRng>,
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

    pub(crate) async fn import_account_uivk(
        &mut self,
        name: &str,
        uivk: &UnifiedIncomingViewingKey,
        birthday: u64,
        key_source: Option<&str>,
        client: &mut CompactTxStreamerClient<Channel>,
    ) -> Result<Account, Error> {
        if birthday == 0 {
            return Err(Error::InvalidArgument(
                "Account birthday height must be greater than 0.".to_string(),
            ));
        }

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
            .import_account_uivk(name, uivk, &birthday, key_source)?)
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

/// `NERDBANK_ZCASH_TREE_WRITE_BACK=0|false|off|no` selects the immediate SQLite shard-tree
/// writes. Any other value, including an unset variable, keeps the write-back cache.
fn commitment_tree_write_back_from_env() -> bool {
    match std::env::var("NERDBANK_ZCASH_TREE_WRITE_BACK") {
        Ok(value) => !matches!(
            value.to_ascii_lowercase().as_str(),
            "0" | "false" | "off" | "no"
        ),
        Err(_) => true,
    }
}

fn get_db_internal<P: AsRef<Path>>(
    data_file: P,
    network: Network,
    init: bool,
) -> Result<Db, Error> {
    if init && let Some(data_dir) = data_file.as_ref().parent() {
        fs::create_dir_all(data_dir)?;
    }

    let mut data = WalletDb::for_path(data_file, network, SystemClock, WalletRng::default())?;
    if !commitment_tree_write_back_from_env() {
        data.set_commitment_tree_write_back(false);
    }

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
    use zcash_client_backend::data_api::{Account as _, WalletRead};

    #[tokio_shared_rt::test]
    async fn test_init() {
        let wallet_dir = testdir!();
        let data_file = wallet_dir.join("wallet.sqlite");
        Db::init(data_file, Network::TestNetwork).unwrap();
    }

    /// A wallet file produced by `zcash_client_sqlite` 0.22.0 (the submodule commit
    /// `3e80bc9b251214415f74257f5fe7b4a090b38a35` that `main` used before this bump).
    ///
    /// Built with the same features this crate enables (`orchard`, `transparent-inputs`,
    /// `unstable`, `multicore`): `init_wallet_db` followed by `import_account_hd` for
    /// seed `[0xab; 32]`, ZIP 32 account index 0, on testnet, with a birthday at Sapling
    /// activation and a prior block hash of all zeros.
    const WALLET_SQLITE_0_22: &[u8] =
        include_bytes!("../testdata/wallet-zcash-client-sqlite-0.22.sqlite");

    /// UFVK string the 0.22 build produced for the account in [`WALLET_SQLITE_0_22`].
    const WALLET_0_22_UFVK: &str = "uviewtest1satqu746lhddzevth48yqpmr6d29kz7cjqxa4dz8pl2qxucs47aqzxtd0utk9sujw2larkaagw89jtn4rz5ae0gm0l52cx9rwrey2fn533zncgy8l4d90w996e3nrz9v6t7pwlsrax3d0jc4r37q0m9gq68al4q79v8pk4lvp5y678xd7j823427p6hyzsj4ns03rem5s25esjldj6tlwtjcr8e50zer47qqds3anscs547phkpg4k6hzn6tzgazhzxfs00tx8dgazlyfgtv87mpftwlpeyzsxujrv37g6zk2rne9uxt2kl736nrwlajygkrz4jmqs4uwrffs46epdj2wqu4vamech3puvpjtwm7qnhnf4lpy3mz4p4ltdtwukehmqsyuzc8dthnsex7r4xw02ukusxlamp6js4p28vau2ljuxjzxvh4wc98z3jf8vq63ymfmgwnu889qrsptf6wg97tvvf6pwq27ksf5av2dyqh4ggjtf3d";

    /// Schema migrations added by the 0.23 wallet code, as uppercase hex of the UUID bytes.
    const SQLITE_0_23_MIGRATIONS: &[&str] = &[
        "3EB2409C6B54476EAEBC8F769AA8950F",
        "A7CD7BFD2A924A7BBA9820D61893B260",
        "B72F8199A8A24A079DBD18EA49D8A17D",
        "C9C8EB79846A4403B400D7BC263765B9",
    ];

    fn migration_ids(path: &Path) -> Vec<String> {
        let conn = Connection::open(path).unwrap();
        let mut stmt = conn
            .prepare("SELECT hex(id) FROM schemer_migrations")
            .unwrap();
        let mut ids = stmt
            .query_map([], |row| row.get::<_, String>(0))
            .unwrap()
            .map(|id| id.unwrap())
            .collect::<Vec<_>>();
        ids.sort();
        ids
    }

    #[test]
    fn migrates_wallet_created_by_client_sqlite_0_22() {
        let wallet_dir = testdir!();
        let data_file = wallet_dir.join("wallet.sqlite");
        fs::write(&data_file, WALLET_SQLITE_0_22).unwrap();

        let before = migration_ids(&data_file);
        assert_eq!(before.len(), 72, "0.22 fixture should record 72 migrations");

        let network = Network::TestNetwork;
        let db = Db::init(&data_file, network).unwrap();

        let account_ids = db.data.get_account_ids().unwrap();
        assert_eq!(account_ids.len(), 1);
        let account = db
            .data
            .get_account(account_ids[0])
            .unwrap()
            .expect("imported account");
        assert_eq!(
            account.id().expose_uuid().to_string(),
            "e57c6a1f-82e6-4bbe-8f4b-f6576f075397"
        );
        assert_eq!(account.name(), Some("legacy-0.22"));
        assert_eq!(u32::from(account.birthday_height()), 280_000);
        let encoded = account
            .ufvk()
            .expect("account has a UFVK")
            .encode(&network)
            .expect("UFVK encodes");
        assert_eq!(encoded, WALLET_0_22_UFVK);

        let address_count: i64 = Connection::open(&data_file)
            .unwrap()
            .query_row("SELECT COUNT(*) FROM addresses", [], |row| row.get(0))
            .unwrap();
        assert_eq!(address_count, 25);

        drop(db);

        let after = migration_ids(&data_file);
        assert!(
            before.iter().all(|id| after.contains(id)),
            "migration must keep the 0.22 schema history"
        );
        for id in SQLITE_0_23_MIGRATIONS {
            assert!(
                after.iter().any(|applied| applied.eq_ignore_ascii_case(id)),
                "0.23 migration {id} was not applied; applied {} migrations",
                after.len()
            );
        }
    }
}
