use std::{
    collections::HashMap,
    fs,
    path::{Path, PathBuf},
    sync::{Arc, LazyLock, Mutex as StdMutex},
};

use rand::{rand_core::UnwrapErr, rngs::SysRng};
use rusqlite::{Connection, OptionalExtension, Transaction, TransactionBehavior, named_params};
use secrecy::SecretVec;
use tokio::sync::Mutex;
use tonic::transport::Channel;
use zcash_client_backend::{
    data_api::{
        AccountBirthday, AccountPurpose, WalletRead, WalletWrite,
        scanning::{ScanPriority, ScanRange, spanning_tree::SpanningTree},
    },
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

/// One mutex per wallet file. Sync holds it from the tree-mode check through the scan
/// commit. [`Db::note_spending_key`] holds it while it records the spending flag and the
/// historic rescan, so an in-flight view-only commit cannot land in the middle.
static WALLET_SCAN_LOCKS: LazyLock<StdMutex<HashMap<PathBuf, Arc<Mutex<()>>>>> =
    LazyLock::new(|| StdMutex::new(HashMap::new()));

/// Lock that serializes chain scans of `path` with a spending-key upgrade.
pub(crate) fn wallet_scan_lock(path: &Path) -> Arc<Mutex<()>> {
    let mut locks = WALLET_SCAN_LOCKS.lock().expect("wallet scan lock map");
    locks
        .entry(path.to_path_buf())
        .or_insert_with(|| Arc::new(Mutex::new(())))
        .clone()
}

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

    /// Marks an imported full-viewing-key account as able to spend.
    ///
    /// Returns whether the stored purpose changed. A change also requeues a historic scan from
    /// the account birthday through the newest stored block, because blocks already scanned
    /// without note commitment trees have no witnesses.
    ///
    /// The flag and the rescan commit in one immediate transaction, and the call waits until
    /// no chain scan of this wallet is between its tree-mode check and its commit. A scan that
    /// is still downloading therefore cannot mark a later block scanned without witnesses
    /// after this returns.
    ///
    /// An incoming-viewing-key account has no full viewing key, so it cannot be marked spending.
    pub(crate) fn note_spending_key(&mut self, account_id: AccountUuid) -> Result<bool, Error> {
        let scan_lock = wallet_scan_lock(&self.path);
        let _scan = scan_lock.blocking_lock();
        let mut conn = Connection::open(&self.path)?;
        let tx = conn.transaction_with_behavior(TransactionBehavior::Immediate)?;
        let row = tx
            .query_row(
                "SELECT has_spend_key, ufvk IS NOT NULL, birthday_height
                 FROM accounts WHERE uuid = :uuid",
                named_params![":uuid": account_id.expose_uuid()],
                |row| {
                    Ok((
                        row.get::<_, i64>(0)?,
                        row.get::<_, i64>(1)?,
                        row.get::<_, u32>(2)?,
                    ))
                },
            )
            .optional()?;
        let Some((has_spend_key, has_ufvk, birthday)) = row else {
            return Err(Error::InvalidArgument("Account not found.".to_string()));
        };
        if has_ufvk == 0 {
            return Err(Error::InvalidArgument(
                "An incoming-viewing-key account cannot be upgraded to spending.".to_string(),
            ));
        }
        if has_spend_key != 0 {
            return Ok(false);
        }
        let max_height: Option<u32> =
            tx.query_row("SELECT MAX(height) FROM blocks", [], |row| row.get(0))?;
        if let Some(max_height) = max_height {
            let start = BlockHeight::from_u32(birthday);
            let end = BlockHeight::from_u32(max_height) + 1;
            if start < end {
                queue_historic_rescan(&tx, start..end)?;
            }
        }
        tx.execute(
            "UPDATE accounts SET has_spend_key = 1 WHERE uuid = :uuid",
            named_params![":uuid": account_id.expose_uuid()],
        )?;
        tx.commit()?;
        Ok(true)
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

/// Replaces scanned coverage of `range` with [`ScanPriority::Historic`].
///
/// Same merge as `WalletDb::queue_rescans`: a higher priority than historic is kept, and
/// the replacement commits with the caller's transaction.
fn queue_historic_rescan(
    tx: &Transaction<'_>,
    range: std::ops::Range<BlockHeight>,
) -> Result<(), Error> {
    if range.is_empty() {
        return Ok(());
    }

    let mut stmt = tx.prepare(
        "SELECT block_range_start, block_range_end, priority
         FROM scan_queue
         WHERE NOT (block_range_start > :end OR :start > block_range_end)
         ORDER BY block_range_end",
    )?;
    let mut rows = stmt.query(named_params![
        ":start": u32::from(range.start),
        ":end": u32::from(range.end),
    ])?;
    let mut tree: Option<SpanningTree> = None;
    let mut ends = Vec::new();
    while let Some(row) = rows.next()? {
        let entry = scan_range_from_row(row)?;
        ends.push(u32::from(entry.block_range().end));
        tree = Some(match tree {
            Some(current) => current.insert(entry, true),
            None => SpanningTree::Leaf(entry),
        });
    }
    drop(rows);
    drop(stmt);

    let historic = ScanRange::from_parts(range, ScanPriority::Historic);
    let tree = match tree {
        Some(current) => current.insert(historic, true),
        None => SpanningTree::Leaf(historic),
    };
    for end in ends {
        tx.execute("DELETE FROM scan_queue WHERE block_range_end = ?1", [end])?;
    }
    for entry in tree.into_vec() {
        if entry.is_empty() {
            continue;
        }
        tx.execute(
            "INSERT INTO scan_queue (block_range_start, block_range_end, priority)
             VALUES (?1, ?2, ?3)",
            (
                u32::from(entry.block_range().start),
                u32::from(entry.block_range().end),
                priority_code(entry.priority()),
            ),
        )?;
    }
    Ok(())
}

fn scan_range_from_row(row: &rusqlite::Row<'_>) -> Result<ScanRange, Error> {
    let start = BlockHeight::from_u32(row.get(0)?);
    let end = BlockHeight::from_u32(row.get(1)?);
    let priority = parse_priority_code(row.get(2)?)
        .ok_or_else(|| Error::Internal("scan_queue contains an unknown priority".to_string()))?;
    Ok(ScanRange::from_parts(start..end, priority))
}

fn priority_code(priority: ScanPriority) -> i64 {
    match priority {
        ScanPriority::Ignored => 0,
        ScanPriority::Scanned => 10,
        ScanPriority::Historic => 20,
        ScanPriority::OpenAdjacent => 30,
        ScanPriority::FoundNote => 40,
        ScanPriority::ChainTip => 50,
        ScanPriority::Verify => 60,
    }
}

fn parse_priority_code(code: i64) -> Option<ScanPriority> {
    match code {
        0 => Some(ScanPriority::Ignored),
        10 => Some(ScanPriority::Scanned),
        20 => Some(ScanPriority::Historic),
        30 => Some(ScanPriority::OpenAdjacent),
        40 => Some(ScanPriority::FoundNote),
        50 => Some(ScanPriority::ChainTip),
        60 => Some(ScanPriority::Verify),
        _ => None,
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
    use rusqlite::Connection;
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

    /// Recording a spending key for a stored full viewing key flips the account to
    /// `Spending`, turns tree maintenance on, and replaces scanned history with a historic
    /// rescan. An incoming viewing key cannot be upgraded. A second call is a no-op.
    #[test]
    fn note_spending_key_upgrades_a_view_only_ufvk_and_requeues_history() {
        let network = Network::TestNetwork;
        let wallet_dir = testdir!();
        let mut db = Db::init(wallet_dir.join("wallet.sqlite"), network).unwrap();
        let birthday = viewing_birthday();
        let ufvk = ufvk_from_seed(network, &[0x44; 32]);
        let account = db
            .data
            .import_account_ufvk("view", &ufvk, &birthday, AccountPurpose::ViewOnly, None)
            .unwrap();
        assert!(matches!(account.purpose(), AccountPurpose::ViewOnly));
        assert!(!db.data.maintains_note_commitment_trees().unwrap());

        let conn = Connection::open(&db.path).unwrap();
        let birthday_height: u32 = conn
            .query_row(
                "SELECT birthday_height FROM accounts WHERE uuid = ?1",
                [account.id().expose_uuid()],
                |row| row.get(0),
            )
            .unwrap();
        let scanned_height = birthday_height + 4;
        conn.execute(
            "INSERT INTO blocks (height, hash, time, sapling_tree) VALUES (?1, ?2, 0, ?3)",
            (scanned_height, vec![0u8; 32], vec![0u8]),
        )
        .unwrap();
        // Pretend those blocks were already scanned without witnesses.
        conn.execute("UPDATE scan_queue SET priority = 10", [])
            .unwrap();
        drop(conn);

        assert!(db.note_spending_key(account.id()).unwrap());
        assert!(db.data.maintains_note_commitment_trees().unwrap());
        let reloaded = db.data.get_account(account.id()).unwrap().unwrap();
        assert!(matches!(
            reloaded.purpose(),
            AccountPurpose::Spending { derivation: None }
        ));

        let conn = Connection::open(&db.path).unwrap();
        let covered: i64 = conn
            .query_row(
                "SELECT COUNT(*) FROM scan_queue
                 WHERE priority = 20
                   AND block_range_start <= ?1
                   AND block_range_end >= ?2",
                (birthday_height, scanned_height + 1),
                |row| row.get(0),
            )
            .unwrap();
        assert_eq!(
            covered, 1,
            "birthday through the newest stored block must be queued as historic"
        );
        drop(conn);

        assert!(!db.note_spending_key(account.id()).unwrap());

        // A second wallet avoids the placeholder block row, which is not a real tree.
        let mut ivk_db = Db::init(wallet_dir.join("ivk.sqlite"), network).unwrap();
        let ivk_ufvk = ufvk_from_seed(network, &[0x55; 32]);
        let ivk = ivk_db
            .data
            .import_account_uivk(
                "ivk",
                &ivk_ufvk.to_unified_incoming_viewing_key(),
                &birthday,
                None,
            )
            .unwrap();
        let err = ivk_db.note_spending_key(ivk.id()).unwrap_err();
        assert!(matches!(err, Error::InvalidArgument(_)));
        let still_view = ivk_db.data.get_account(ivk.id()).unwrap().unwrap();
        assert!(matches!(still_view.purpose(), AccountPurpose::ViewOnly));
        assert!(!ivk_db.data.maintains_note_commitment_trees().unwrap());
    }
}
