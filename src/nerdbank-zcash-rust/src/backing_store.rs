use std::{
    fs,
    path::{Path, PathBuf},
};

use rand::{rand_core::UnwrapErr, rngs::SysRng};
use rusqlite::Connection;
use secrecy::SecretVec;
use tonic::transport::Channel;
use zcash_client_backend::{
    data_api::{AccountBirthday, AccountPurpose, WalletRead, WalletWrite},
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
use zcash_primitives::block::BlockHash;
use zcash_protocol::consensus::{BlockHeight, Network};
use zip32::DiversifierIndex;

use crate::{block_source::BlockCache, error::Error, sync_timing::SyncTiming};

/// Operating-system RNG stored on [`WalletDb`].
///
/// `rand` 0.10's [`SysRng`] reports operating-system failures as errors.
/// [`WalletDb`] requires [`rand::Rng`], so this wrapper panics if the operating
/// system cannot provide entropy.
pub(crate) type WalletRng = UnwrapErr<SysRng>;

pub(crate) struct Db {
    pub(crate) data: WalletDb<Connection, Network, SystemClock, WalletRng>,
    pub(crate) blocks: BlockCache,
    /// Wallet file path, used when a query needs a second connection.
    pub(crate) path: PathBuf,
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

        // A seed account can spend. Its birthday needs the prior note commitment frontiers.
        let birthday =
            chain_state_birthday(client, birthday.saturating_sub(1), recover_until).await?;

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
        let importing_spending = matches!(purpose, AccountPurpose::Spending { .. });
        let birthday = self
            .birthday_for_import(client, birthday, None, importing_spending)
            .await?;

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
        // A UIVK cannot spend. Tree state is still required when this wallet already
        // maintains note commitment trees for a spending account.
        let birthday = self
            .birthday_for_import(client, birthday, None, false)
            .await?;

        Ok(self
            .data
            .import_account_uivk(name, uivk, &birthday, key_source)?)
    }

    /// Builds the birthday for an account import.
    ///
    /// Spending imports, and view-only imports into a wallet that already has a spending
    /// account, fetch `GetTreeState` so the note commitment frontiers are real. A wallet that
    /// is empty or contains only view-only accounts uses [`AccountBirthday::from_viewing_parts`]
    /// and does not fetch a tree state. The prior block hash comes from that block's compact
    /// form. An empty wallet reports that it maintains trees, so the spending check is based on
    /// the accounts already stored, not on that default.
    async fn birthday_for_import(
        &self,
        client: &mut CompactTxStreamerClient<Channel>,
        birthday: u64,
        recover_until: Option<BlockHeight>,
        importing_spending: bool,
    ) -> Result<AccountBirthday, Error> {
        if importing_spending || self.has_spending_account()? {
            if birthday == 0 {
                return chain_state_birthday(client, 0, recover_until).await;
            }
            return chain_state_birthday(client, birthday - 1, recover_until).await;
        }

        viewing_birthday(client, birthday, recover_until).await
    }

    /// `true` when the wallet already contains an account that can spend.
    ///
    /// An empty wallet returns `false`. [`WalletRead::maintains_note_commitment_trees`] returns
    /// `true` for an empty wallet, which is the right scan default and the wrong answer here.
    fn has_spending_account(&self) -> Result<bool, Error> {
        if self.data.get_account_ids()?.is_empty() {
            return Ok(false);
        }
        Ok(self.data.maintains_note_commitment_trees()?)
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

/// Fetches `GetTreeState` for `prior_height` and builds a spending birthday from it.
///
/// The request tells the server which height the wallet will start scanning. Spending wallets
/// need the frontiers in that response.
async fn chain_state_birthday(
    client: &mut CompactTxStreamerClient<Channel>,
    prior_height: u64,
    recover_until: Option<BlockHeight>,
) -> Result<AccountBirthday, Error> {
    let request = service::BlockId {
        height: prior_height,
        ..Default::default()
    };
    let treestate = client.get_tree_state(request).await?.into_inner();
    Ok(AccountBirthday::from_treestate(treestate, recover_until)?)
}

/// Birthday for a wallet that will scan without note commitment trees.
///
/// The prior block hash is read from the compact block at `birthday - 1`. Frontiers stay empty.
async fn viewing_birthday(
    client: &mut CompactTxStreamerClient<Channel>,
    birthday: u64,
    recover_until: Option<BlockHeight>,
) -> Result<AccountBirthday, Error> {
    if birthday == 0 {
        return Err(Error::InvalidArgument(
            "Account birthday height must be greater than 0.".to_string(),
        ));
    }

    let prior_height = birthday - 1;
    let prior_hash = prior_compact_block_hash(client, prior_height).await?;
    let prior_height = u32::try_from(prior_height).map_err(|_| Error::InvalidHeight)?;
    Ok(AccountBirthday::from_viewing_parts(
        BlockHeight::from_u32(prior_height),
        prior_hash,
        recover_until,
    ))
}

/// Hash of the compact block at `height`, without asking for a note commitment tree state.
async fn prior_compact_block_hash(
    client: &mut CompactTxStreamerClient<Channel>,
    height: u64,
) -> Result<BlockHash, Error> {
    let block_id = service::BlockId {
        height,
        ..Default::default()
    };
    let range = service::BlockRange {
        start: Some(block_id.clone()),
        end: Some(block_id),
        pool_types: vec![],
    };
    let block = client
        .get_block_range(range)
        .await?
        .into_inner()
        .message()
        .await?
        .ok_or_else(|| {
            Error::Internal(format!(
                "lightwalletd returned no compact block at height {height}"
            ))
        })?;
    if block.hash.len() != 32 && block.header.is_empty() {
        return Err(Error::Internal(format!(
            "compact block at height {height} has no block hash"
        )));
    }
    Ok(block.hash())
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
    let _timer = SyncTiming::db_open_timer();
    if init && let Some(data_dir) = data_file.as_ref().parent() {
        fs::create_dir_all(data_dir)?;
    }

    let path = data_file.as_ref().to_path_buf();
    let mut data = WalletDb::for_path(&path, network, SystemClock, WalletRng::default())?;
    if !commitment_tree_write_back_from_env() {
        data.set_commitment_tree_write_back(false);
    }

    if init {
        init_wallet_db(&mut data, None)?;
    }

    Ok(Db {
        data,
        blocks: BlockCache::new(),
        path,
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

    fn viewing_birthday() -> AccountBirthday {
        AccountBirthday::from_viewing_parts(BlockHeight::from_u32(1), BlockHash([0; 32]), None)
    }

    fn ufvk_from_seed(network: Network, seed: &[u8]) -> UnifiedFullViewingKey {
        UnifiedSpendingKey::from_seed(&network, seed, zip32::AccountId::ZERO)
            .expect("seed derives")
            .to_unified_full_viewing_key()
    }

    /// View-only imports record `AccountPurpose::ViewOnly` and, while they are the whole wallet,
    /// skip note commitment trees. A spending account in the same wallet turns tree maintenance
    /// back on. Seed accounts are spending accounts: the seed is stored and the purpose is
    /// `Spending`.
    #[test]
    fn view_only_wallet_skips_trees_until_a_spending_account_is_added() {
        let network = Network::TestNetwork;
        let wallet_dir = testdir!();
        let mut db = Db::init(wallet_dir.join("wallet.sqlite"), network).unwrap();
        assert!(
            !db.has_spending_account().unwrap(),
            "an empty wallet has no spending account"
        );
        // The scan default for an empty wallet is still to maintain trees.
        assert!(db.data.maintains_note_commitment_trees().unwrap());

        let birthday = viewing_birthday();
        let view_ufvk = ufvk_from_seed(network, &[0x11; 32]);
        let view = db
            .data
            .import_account_ufvk(
                "view",
                &view_ufvk,
                &birthday,
                AccountPurpose::ViewOnly,
                None,
            )
            .unwrap();
        assert!(matches!(view.purpose(), AccountPurpose::ViewOnly));
        assert!(!db.has_spending_account().unwrap());
        assert!(!db.data.maintains_note_commitment_trees().unwrap());

        let ivk_ufvk = ufvk_from_seed(network, &[0x33; 32]);
        let ivk = db
            .data
            .import_account_uivk(
                "ivk",
                &ivk_ufvk.to_unified_incoming_viewing_key(),
                &birthday,
                None,
            )
            .unwrap();
        assert!(matches!(ivk.purpose(), AccountPurpose::ViewOnly));
        assert!(ivk.ufvk().is_none());
        assert!(!db.data.maintains_note_commitment_trees().unwrap());

        let spend_ufvk = ufvk_from_seed(network, &[0x22; 32]);
        let spend = db
            .data
            .import_account_ufvk(
                "spend",
                &spend_ufvk,
                &birthday,
                AccountPurpose::Spending { derivation: None },
                None,
            )
            .unwrap();
        assert!(matches!(
            spend.purpose(),
            AccountPurpose::Spending { derivation: None }
        ));
        assert!(db.has_spending_account().unwrap());
        assert!(db.data.maintains_note_commitment_trees().unwrap());
    }

    #[test]
    fn seed_account_is_a_spending_account() {
        let network = Network::TestNetwork;
        let wallet_dir = testdir!();
        let mut db = Db::init(wallet_dir.join("wallet.sqlite"), network).unwrap();
        let seed = SecretVec::new(vec![0xab; 64]);
        let (account, _) = db
            .data
            .import_account_hd(
                "hd",
                &seed,
                zip32::AccountId::ZERO,
                &viewing_birthday(),
                None,
            )
            .unwrap();
        assert!(matches!(
            account.purpose(),
            AccountPurpose::Spending {
                derivation: Some(_)
            }
        ));
        assert!(db.has_spending_account().unwrap());
        assert!(db.data.maintains_note_commitment_trees().unwrap());
    }
}
