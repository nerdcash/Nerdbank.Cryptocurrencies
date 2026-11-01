//! Functions for querying transactions by recipient address.

use orchard::keys::Scope;
use rusqlite::{Connection, OptionalExtension, named_params};
use uuid::Uuid;
use zcash_address::ZcashAddress;
use zcash_client_backend::{data_api::WalletRead, encoding::AddressCodec};
use zcash_client_sqlite::{AccountUuid, error::SqliteClientError};
use zcash_keys::address::UnifiedAddress;
use zcash_protocol::{
    PoolType,
    consensus::{Network, Parameters},
    memo::Memo,
};

use crate::{
    backing_store::Db,
    error::Error,
    interop::{Pool, Transaction, TransactionNote},
    sql_statements::GET_INCOMING_PAYMENTS_SQL,
};

/// Information about a receiver extracted from an address, including the
/// account it belongs to and the diversifier that can be used to filter in SQL.
struct ReceiverInfo {
    account_uuid: AccountUuid,
    /// The diversifier bytes for shielded receivers (Orchard/Ironwood/Sapling).
    /// For transparent, this will be None since transparent uses address strings.
    diversifier: Option<Vec<u8>>,
    /// For transparent addresses, the encoded address string.
    transparent_address: Option<String>,
    /// The receiver pool; Orchard receivers also accept Ironwood payments.
    pool: PoolType,
    /// The shielded receiver's key scope, or None for transparent receivers.
    scope: Option<Scope>,
}

/// Returns all transactions containing incoming payments to any receiver within the given address.
///
/// A ZcashAddress may represent a unified address with multiple receivers.
/// This function extracts all receivers from the address, determines which account
/// they belong to (they must all belong to exactly one account), and then queries
/// the database for payments to those receivers using the diversifier for efficient filtering.
///
/// # Arguments
/// * `db` - The wallet database
/// * `conn` - A database connection
/// * `network` - The Zcash network (mainnet/testnet)
/// * `address` - The address to search for (may be a unified address with multiple receivers)
/// * `starting_block` - The minimum block height to search from (inclusive)
///
/// # Returns
/// A list of unique transactions that contain at least one incoming payment to the specified address.
///
/// # Errors
/// Returns an error if the address receivers don't all belong to exactly one account in the wallet.
pub(crate) fn get_incoming_payments(
    db: &mut Db,
    conn: &mut Connection,
    network: &Network,
    address: &str,
    starting_block: Option<u32>,
) -> Result<Vec<Transaction>, Error> {
    // Parse the address and find which account/diversifier it corresponds to
    let receiver_infos = find_receiver_info(db, conn, network, address)?;

    // Verify all receivers belong to the same account
    let account_uuid = receiver_infos[0].account_uuid;
    for info in &receiver_infos {
        if info.account_uuid != account_uuid {
            return Err(Error::InvalidAddress); // Receivers from different accounts
        }
    }

    let ufvkeys = db.data.get_unified_full_viewing_keys()?;

    // Query for each receiver type and combine results
    let mut all_transactions: Vec<Transaction> = Vec::new();

    // Prepare the SQL statement once and reuse it for each receiver to avoid
    // the overhead of repeatedly preparing the same statement.
    let mut stmt_txs = conn.prepare(GET_INCOMING_PAYMENTS_SQL)?;

    for receiver_info in &receiver_infos {
        let rows = stmt_txs.query_and_then(
            named_params! {
                ":account_uuid": account_uuid.expose_uuid(),
                ":starting_block": starting_block,
                ":diversifier": receiver_info.diversifier.as_ref(),
                ":transparent_address": receiver_info.transparent_address.as_ref(),
                ":output_pool": match receiver_info.pool {
                    PoolType::Transparent => 0,
                    PoolType::SAPLING => 2,
                    PoolType::ORCHARD => 3,
                    PoolType::IRONWOOD => 4
                },
                ":recipient_key_scope": receiver_info.scope.map(|scope| match scope {
                    Scope::External => 0,
                    Scope::Internal => 1
                }),
            },
            |row| -> Result<Transaction, Error> {
                let account_uuid = AccountUuid::from_uuid(row.get("account_uuid")?);
                let output_pool: u32 = row.get("output_pool")?;
                let from_account_uuid = row
                    .get::<_, Option<Uuid>>("from_account_uuid")?
                    .map(AccountUuid::from_uuid);
                let to_account_uuid = row
                    .get::<_, Option<Uuid>>("to_account_uuid")?
                    .map(AccountUuid::from_uuid);
                let mut recipient: Option<String> = row.get("to_address")?;
                let value: u64 = row.get("value")?;
                let memo: Option<Vec<u8>> = row.get("memo")?;
                let memo = memo.unwrap_or_default();

                let output_pool = match output_pool {
                    0 => PoolType::Transparent,
                    2 => PoolType::SAPLING,
                    3 => PoolType::ORCHARD,
                    4 => PoolType::IRONWOOD,
                    _ => {
                        return Err(Error::SqliteClient(SqliteClientError::CorruptedData(
                            format!("Unknown output pool type: {output_pool}"),
                        )));
                    }
                };

                let ufvk = ufvkeys.get(&account_uuid);

                // Work out the receiving address when the sqlite db doesn't record it
                // but we have a diversifier that can regenerate it.
                if recipient.is_none() {
                    let diversifier: Option<Vec<u8>> = row.get("diversifier")?;
                    if let Some(diversifier) = diversifier {
                        let scope = crate::util::parse_recipient_key_scope(
                            row.get("recipient_key_scope")?,
                        )?;
                        recipient = match output_pool {
                            PoolType::SAPLING => ufvk.and_then(|k| {
                                k.sapling().and_then(|s| {
                                    s.to_ivk(scope)
                                        .to_payment_address(sapling::keys::Diversifier(
                                            diversifier.clone().try_into().unwrap(),
                                        ))
                                        .map(|a| a.encode(network))
                                })
                            }),
                            PoolType::ORCHARD | PoolType::IRONWOOD => ufvk.and_then(|k| {
                                k.orchard().map(|o| {
                                    UnifiedAddress::from_receivers(
                                        Some(o.address(
                                            orchard::keys::Diversifier::from_bytes(
                                                diversifier.clone().try_into().unwrap(),
                                            ),
                                            scope,
                                        )),
                                        None,
                                        None,
                                        None,
                                        None,
                                    )
                                    .unwrap()
                                    .encode(network)
                                })
                            }),
                            _ => None,
                        }
                    }
                }

                let mut tx = Transaction {
                    account_id: account_uuid.expose_uuid().as_bytes().to_vec(),
                    txid: row.get::<_, Vec<u8>>("txid")?,
                    mined_height: match row.get("mined_height")? {
                        0 => None,
                        h => Some(h),
                    },
                    expired_unmined: row
                        .get::<_, Option<bool>>("expired_unmined")?
                        .unwrap_or(false),
                    block_time: match row.get::<_, Option<i64>>("block_time")? {
                        Some(v) => Some(
                            time::OffsetDateTime::from_unix_timestamp(v)
                                .map_err(|e| {
                                    Error::SqliteClient(SqliteClientError::CorruptedData(format!(
                                        "Error translating unix timestamp: {e}"
                                    )))
                                })?
                                .into(),
                        ),
                        None => None,
                    },
                    fee: row.get::<_, Option<u64>>("fee_paid")?,
                    account_balance_delta: row.get("account_balance_delta")?,
                    incoming: Vec::new(),
                    outgoing: Vec::new(),
                    change: Vec::new(),
                };

                let note = TransactionNote {
                    value,
                    recipient: recipient.clone().unwrap_or_default(),
                    pool: match output_pool {
                        PoolType::Transparent => Pool::Transparent,
                        PoolType::SAPLING => Pool::Sapling,
                        PoolType::ORCHARD => Pool::Orchard,
                        PoolType::IRONWOOD => Pool::Ironwood,
                    },
                    memo: if memo.is_empty() {
                        None
                    } else {
                        Some(memo.clone())
                    },
                };

                // We establish change by all the following criteria holding true:
                // * the recipient is to the same account
                // * the recipient is shielded (since change will never be sent to the transparent pool).
                // * the memo does not contain user text,
                let is_change = to_account_uuid == from_account_uuid
                    && matches!(output_pool, PoolType::Shielded(_))
                    && Memo::from_bytes(&memo).is_ok_and(|m| !matches!(m, Memo::Text(_)));

                if is_change {
                    tx.change.push(note);
                } else {
                    tx.incoming.push(note);
                }

                Ok(tx)
            },
        )?;

        for row_result in rows {
            let row = row_result?;
            // Merge with existing transaction if same txid, otherwise add new
            if let Some(existing) = all_transactions
                .iter_mut()
                .find(|t| t.account_id == row.account_id && t.txid == row.txid)
            {
                existing.incoming.extend(row.incoming);
                existing.change.extend(row.change);
            } else {
                all_transactions.push(row);
            }
        }
    }

    // Sort by mined height
    all_transactions.sort_by_key(|a| a.mined_height);

    Ok(all_transactions)
}

/// Find the account and diversifier information for each receiver in the given address.
///
/// This function parses the address, extracts its receivers, and for each receiver
/// determines which account in the wallet owns it and what diversifier was used.
fn find_receiver_info(
    db: &mut Db,
    conn: &Connection,
    network: &Network,
    address: &str,
) -> Result<Vec<ReceiverInfo>, Error> {
    let parsed = ZcashAddress::try_from_encoded(address).map_err(|_| Error::InvalidAddress)?;

    let addr = parsed
        .convert_if_network::<zcash_keys::address::Address>(network.network_type())
        .map_err(|_| Error::InvalidAddress)?;

    let ufvkeys = db.data.get_unified_full_viewing_keys()?;
    let mut receiver_infos = Vec::new();

    match addr {
        zcash_keys::address::Address::Unified(ua) => {
            // Try to find the account that owns this unified address by checking each receiver
            if let Some(orchard_addr) = ua.orchard()
                && let Some((account_uuid, diversifier, scope)) =
                    find_orchard_receiver_info(&ufvkeys, orchard_addr)
            {
                receiver_infos.push(ReceiverInfo {
                    account_uuid,
                    diversifier: Some(diversifier),
                    transparent_address: None,
                    pool: PoolType::ORCHARD,
                    scope: Some(scope),
                });
            }

            if let Some(sapling_addr) = ua.sapling()
                && let Some((account_uuid, diversifier, scope)) =
                    find_sapling_receiver_info(&ufvkeys, sapling_addr)
            {
                receiver_infos.push(ReceiverInfo {
                    account_uuid,
                    diversifier: Some(diversifier),
                    transparent_address: None,
                    pool: PoolType::SAPLING,
                    scope: Some(scope),
                });
            }

            if let Some(transparent_addr) = ua.transparent() {
                // For transparent, we need to find the account by checking if the address
                // can be derived from any account's transparent key
                if let Some((account_uuid, addr_str)) =
                    find_transparent_receiver_info(conn, network, transparent_addr)?
                {
                    receiver_infos.push(ReceiverInfo {
                        account_uuid,
                        diversifier: None,
                        transparent_address: Some(addr_str),
                        pool: PoolType::Transparent,
                        scope: None,
                    });
                }
            }
        }
        zcash_keys::address::Address::Sapling(pa) => {
            if let Some((account_uuid, diversifier, scope)) =
                find_sapling_receiver_info(&ufvkeys, &pa)
            {
                receiver_infos.push(ReceiverInfo {
                    account_uuid,
                    diversifier: Some(diversifier),
                    transparent_address: None,
                    pool: PoolType::SAPLING,
                    scope: Some(scope),
                });
            }
        }
        zcash_keys::address::Address::Transparent(taddr) => {
            if let Some((account_uuid, addr_str)) =
                find_transparent_receiver_info(conn, network, &taddr)?
            {
                receiver_infos.push(ReceiverInfo {
                    account_uuid,
                    diversifier: None,
                    transparent_address: Some(addr_str),
                    pool: PoolType::Transparent,
                    scope: None,
                });
            }
        }
        zcash_keys::address::Address::Tex(data) => {
            let taddr = zcash_transparent::address::TransparentAddress::PublicKeyHash(data);
            if let Some((account_uuid, addr_str)) =
                find_transparent_receiver_info(conn, network, &taddr)?
            {
                receiver_infos.push(ReceiverInfo {
                    account_uuid,
                    diversifier: None,
                    transparent_address: Some(addr_str),
                    pool: PoolType::Transparent,
                    scope: None,
                });
            }
        }
    }

    if receiver_infos.is_empty() {
        return Err(Error::InvalidAddress);
    }

    Ok(receiver_infos)
}

/// Find the account, diversifier, and key scope for an Orchard receiver.
fn find_orchard_receiver_info(
    ufvkeys: &std::collections::HashMap<AccountUuid, zcash_keys::keys::UnifiedFullViewingKey>,
    orchard_addr: &orchard::Address,
) -> Option<(AccountUuid, Vec<u8>, Scope)> {
    for (account_uuid, ufvk) in ufvkeys {
        if let Some(fvk) = ufvk.orchard()
            && let Some(scope) = fvk.scope_for_address(orchard_addr)
        {
            return Some((
                *account_uuid,
                orchard_addr.diversifier().as_array().to_vec(),
                scope,
            ));
        }
    }
    None
}

/// Find the account, diversifier, and key scope for a Sapling receiver.
fn find_sapling_receiver_info(
    ufvkeys: &std::collections::HashMap<AccountUuid, zcash_keys::keys::UnifiedFullViewingKey>,
    sapling_addr: &sapling::PaymentAddress,
) -> Option<(AccountUuid, Vec<u8>, Scope)> {
    for (account_uuid, ufvk) in ufvkeys {
        if let Some(dfvk) = ufvk.sapling() {
            for scope in [Scope::External, Scope::Internal] {
                if dfvk
                    .to_ivk(scope)
                    .to_payment_address(*sapling_addr.diversifier())
                    .as_ref()
                    == Some(sapling_addr)
                {
                    return Some((*account_uuid, sapling_addr.diversifier().0.to_vec(), scope));
                }
            }
        }
    }
    None
}

/// Find the account for a transparent receiver by querying the database.
/// Returns the account UUID and the encoded address string.
fn find_transparent_receiver_info(
    conn: &Connection,
    network: &Network,
    transparent_addr: &zcash_transparent::address::TransparentAddress,
) -> Result<Option<(AccountUuid, String)>, Error> {
    // Encode the transparent address to its string form
    let addr_str = transparent_addr.encode(network);

    // Query the database to find which account owns this address.
    // First check the addresses table which contains all known addresses for accounts,
    // then fallback to transparent_received_outputs for addresses that have received funds.
    let mut stmt = conn.prepare(
        "SELECT a.uuid
         FROM addresses addr
         JOIN accounts a ON a.id = addr.account_id
         WHERE addr.cached_transparent_receiver_address = :address
         UNION
         SELECT a.uuid
         FROM transparent_received_outputs tro
         JOIN accounts a ON a.id = tro.account_id
         WHERE tro.address = :address
         LIMIT 1",
    )?;

    let result = stmt
        .query_row(named_params! { ":address": &addr_str }, |row| {
            let uuid: Uuid = row.get(0)?;
            Ok(AccountUuid::from_uuid(uuid))
        })
        .optional()?;

    Ok(result.map(|account_uuid| (account_uuid, addr_str)))
}

#[cfg(test)]
mod tests {
    use super::*;
    use zcash_client_backend::data_api::{Account, AccountBirthday, AccountPurpose, WalletWrite};
    use zcash_client_backend::keys::UnifiedSpendingKey;
    use zcash_primitives::block::BlockHash;
    use zcash_protocol::consensus::BlockHeight;

    struct QueryFixture {
        db: Db,
        conn: Connection,
        account_uuid: AccountUuid,
        account_id: i64,
        address: String,
        diversifier: Vec<u8>,
        ufvk: zcash_keys::keys::UnifiedFullViewingKey,
    }

    impl QueryFixture {
        fn new() -> Self {
            Self::new_with_spending_keys(false)
        }

        fn new_with_spending_keys(spending: bool) -> Self {
            let file = testdir::testdir!().join("wallet.sqlite");
            let mut db = Db::init(&file, Network::TestNetwork).unwrap();
            let ufvk = UnifiedSpendingKey::from_seed(
                &Network::TestNetwork,
                &[0x51; 64],
                zip32::AccountId::ZERO,
            )
            .unwrap()
            .to_unified_full_viewing_key();
            let birthday = AccountBirthday::from_viewing_parts(
                BlockHeight::from_u32(1),
                BlockHash([0; 32]),
                None,
            );
            let account_uuid = if spending {
                db.data
                    .create_account(
                        "spend",
                        &secrecy::SecretVec::new(vec![0x51; 64]),
                        &birthday,
                        None,
                    )
                    .unwrap()
                    .0
            } else {
                db.data
                    .import_account_ufvk("view", &ufvk, &birthday, AccountPurpose::ViewOnly, None)
                    .unwrap()
                    .id()
            };
            let receiver = ufvk.orchard().unwrap().address_at(0u32, Scope::External);
            let address = UnifiedAddress::from_receivers(Some(receiver), None, None, None, None)
                .unwrap()
                .encode(&Network::TestNetwork);
            let conn = Connection::open(&file).unwrap();
            let account_id = conn
                .query_row(
                    "SELECT id FROM accounts WHERE uuid = ?1",
                    [account_uuid.expose_uuid()],
                    |row| row.get(0),
                )
                .unwrap();
            conn.execute_batch(
                "INSERT INTO blocks (height, hash, time, sapling_tree)
                 VALUES (100, zeroblob(32), 1000, X'000000');
                 INSERT INTO transactions (id_tx, txid, block, mined_height, min_observed_height)
                 VALUES (1, zeroblob(32), 100, 100, 100);",
            )
            .unwrap();
            Self {
                db,
                conn,
                account_uuid,
                account_id,
                address,
                diversifier: receiver.diversifier().as_array().to_vec(),
                ufvk,
            }
        }

        fn add_note(&self, pool: Pool, sent: bool) {
            self.add_scoped_note(pool, sent, Scope::External, 0);
        }

        fn receiver(&self, pool: Pool, scope: Scope) -> (String, Vec<u8>) {
            match pool {
                Pool::Sapling => {
                    let dfvk = self.ufvk.sapling().unwrap();
                    let diversifier = *dfvk.default_address().1.diversifier();
                    let receiver = dfvk.to_ivk(scope).to_payment_address(diversifier).unwrap();
                    (
                        receiver.encode(&Network::TestNetwork),
                        diversifier.0.to_vec(),
                    )
                }
                Pool::Orchard | Pool::Ironwood => {
                    let receiver = self.ufvk.orchard().unwrap().address(
                        orchard::keys::Diversifier::from_bytes(
                            self.diversifier.clone().try_into().unwrap(),
                        ),
                        scope,
                    );
                    let address =
                        UnifiedAddress::from_receivers(Some(receiver), None, None, None, None)
                            .unwrap()
                            .encode(&Network::TestNetwork);
                    (address, self.diversifier.clone())
                }
                _ => unreachable!(),
            }
        }

        fn add_scoped_note(&self, pool: Pool, sent: bool, scope: Scope, output_index: u32) {
            let (table, code, index_column, randomness_columns, randomness, version) = match pool {
                Pool::Sapling => (
                    "sapling_received_notes",
                    2,
                    "output_index",
                    "rcm",
                    "zeroblob(32)",
                    "",
                ),
                Pool::Orchard => (
                    "orchard_received_notes",
                    3,
                    "action_index",
                    "rho, rseed",
                    "zeroblob(32), zeroblob(32)",
                    "",
                ),
                Pool::Ironwood => (
                    "ironwood_received_notes",
                    4,
                    "action_index",
                    "rho, rseed",
                    "zeroblob(32), zeroblob(32)",
                    ", note_version",
                ),
                _ => unreachable!(),
            };
            let (_, diversifier) = self.receiver(pool, scope);
            let scope = match scope {
                Scope::External => 0,
                Scope::Internal => 1,
            };
            self.conn
                .execute(
                    &format!(
                        "INSERT INTO {table}
                    (transaction_id, {index_column}, account_id, diversifier, value,
                     {randomness_columns}, is_change, memo, recipient_key_scope{version})
                    VALUES (1, ?5, ?1, ?2, 20000, {randomness}, ?3, X'F6', ?4{})",
                        if matches!(pool, Pool::Ironwood) {
                            ", 2"
                        } else {
                            ""
                        }
                    ),
                    rusqlite::params![self.account_id, diversifier, sent, scope, output_index],
                )
                .unwrap();
            if sent {
                self.conn
                    .execute(
                        "INSERT INTO sent_notes
                     (transaction_id, output_pool, output_index, from_account_id,
                      to_account_id, value, memo)
                     VALUES (1, ?1, ?3, ?2, ?2, 20000, X'F6')",
                        rusqlite::params![code, self.account_id, output_index],
                    )
                    .unwrap();
            }
        }

        fn transactions(&mut self) -> Vec<Transaction> {
            crate::sync::get_transactions(
                &mut self.db,
                &mut self.conn,
                &Network::TestNetwork,
                Some(self.account_uuid),
                None,
                None,
            )
            .unwrap()
        }

        fn incoming(&mut self) -> Vec<Transaction> {
            get_incoming_payments(
                &mut self.db,
                &mut self.conn,
                &Network::TestNetwork,
                &self.address,
                None,
            )
            .unwrap()
        }
    }

    #[test]
    fn ironwood_queries_preserve_both_pools_and_reconstruct_recipients() {
        let mut fixture = QueryFixture::new();
        fixture.add_note(Pool::Orchard, false);
        fixture.add_note(Pool::Ironwood, false);
        for txs in [fixture.transactions(), fixture.incoming()] {
            assert_eq!(txs.len(), 1);
            assert_eq!(txs[0].incoming.len(), 2);
            assert!(txs[0].outgoing.is_empty());
            assert!(txs[0].change.is_empty());
            assert_eq!(txs[0].mined_height, Some(100));
            assert_eq!(txs[0].incoming[0].pool, Pool::Orchard);
            assert_eq!(txs[0].incoming[1].pool, Pool::Ironwood);
            for note in &txs[0].incoming {
                assert_eq!(note.recipient, fixture.address);
                assert_eq!(note.value, 20000);
            }
        }
        assert!(
            get_incoming_payments(
                &mut fixture.db,
                &mut fixture.conn,
                &Network::TestNetwork,
                &fixture.address,
                Some(101)
            )
            .unwrap()
            .is_empty()
        );
        assert!(
            crate::sync::get_transactions(
                &mut fixture.db,
                &mut fixture.conn,
                &Network::TestNetwork,
                Some(fixture.account_uuid),
                Some(101),
                None
            )
            .unwrap()
            .is_empty()
        );
    }

    #[test]
    fn ironwood_queries_preserve_equal_valued_incoming_outputs() {
        assert_equal_valued_outputs(false);
    }

    #[test]
    fn ironwood_queries_preserve_equal_valued_change_outputs() {
        assert_equal_valued_outputs(true);
    }

    fn assert_equal_valued_outputs(change: bool) {
        let mut fixture = QueryFixture::new();
        fixture.add_scoped_note(Pool::Ironwood, change, Scope::External, 0);
        fixture.add_scoped_note(Pool::Ironwood, change, Scope::External, 1);
        for txs in [fixture.transactions(), fixture.incoming()] {
            assert_eq!(txs.len(), 1);
            let notes = if change {
                assert!(txs[0].incoming.is_empty());
                &txs[0].change
            } else {
                assert!(txs[0].change.is_empty());
                &txs[0].incoming
            };
            assert_eq!(notes.len(), 2);
            assert_eq!(notes.iter().map(|note| note.value).sum::<u64>(), 40_000);
            assert!(
                notes
                    .iter()
                    .all(|note| note.pool == Pool::Ironwood && note.recipient == fixture.address)
            );
        }
    }

    #[test]
    fn ironwood_queries_merge_disjoint_receivers_without_losing_outputs() {
        let mut fixture = QueryFixture::new();
        fixture.diversifier = fixture.receiver(Pool::Sapling, Scope::External).1;
        fixture.add_scoped_note(Pool::Ironwood, false, Scope::External, 0);
        fixture.add_scoped_note(Pool::Ironwood, false, Scope::External, 1);
        fixture.add_note(Pool::Sapling, false);
        let orchard_receiver = fixture.ufvk.orchard().unwrap().address(
            orchard::keys::Diversifier::from_bytes(fixture.diversifier.clone().try_into().unwrap()),
            Scope::External,
        );
        let address = UnifiedAddress::from_receivers(
            Some(orchard_receiver),
            Some(fixture.ufvk.sapling().unwrap().default_address().1),
            None,
            None,
            None,
        )
        .unwrap()
        .encode(&Network::TestNetwork);
        let txs = get_incoming_payments(
            &mut fixture.db,
            &mut fixture.conn,
            &Network::TestNetwork,
            &address,
            None,
        )
        .unwrap();
        assert_eq!(txs.len(), 1);
        assert_eq!(txs[0].incoming.len(), 3);
        assert_eq!(
            txs[0].incoming.iter().map(|note| note.value).sum::<u64>(),
            60_000
        );
        assert_eq!(
            txs[0]
                .incoming
                .iter()
                .filter(|note| note.pool == Pool::Ironwood)
                .count(),
            2
        );
        assert_eq!(
            txs[0]
                .incoming
                .iter()
                .filter(|note| note.pool == Pool::Sapling)
                .count(),
            1
        );
    }

    #[test]
    fn ironwood_queries_classify_change_and_self_payments() {
        let mut fixture = QueryFixture::new();
        fixture.add_note(Pool::Ironwood, true);
        for txs in [fixture.transactions(), fixture.incoming()] {
            assert_eq!(txs[0].change.len(), 1);
            assert_eq!(txs[0].change[0].pool, Pool::Ironwood);
            assert!(txs[0].incoming.is_empty());
            assert!(txs[0].outgoing.is_empty());
        }
        fixture
            .conn
            .execute_batch(
                "UPDATE ironwood_received_notes SET memo = X'68656c6c6f';
             UPDATE sent_notes SET memo = X'68656c6c6f';",
            )
            .unwrap();
        let txs = fixture.transactions();
        assert_eq!(txs[0].incoming.len(), 1);
        assert_eq!(txs[0].outgoing.len(), 1);
        assert!(txs[0].change.is_empty());
        assert_eq!(fixture.incoming()[0].incoming.len(), 1);
    }

    #[test]
    fn ironwood_queries_find_sender_from_spent_notes() {
        assert_sender_from_spent_notes(None);
    }

    #[test]
    fn ironwood_queries_find_sender_from_mixed_orchard_spends() {
        assert_sender_from_spent_notes(Some(Pool::Orchard));
    }

    #[test]
    fn ironwood_queries_find_sender_from_mixed_sapling_spends() {
        assert_sender_from_spent_notes(Some(Pool::Sapling));
    }

    fn assert_sender_from_spent_notes(other_pool: Option<Pool>) {
        let mut fixture = QueryFixture::new();
        fixture.add_note(Pool::Ironwood, false);
        if let Some(pool) = other_pool {
            fixture.add_note(pool, false);
        }
        fixture
            .conn
            .execute_batch(
                "INSERT INTO transactions (id_tx, txid, mined_height, min_observed_height)
             VALUES (2, randomblob(32), 101, 101);
             INSERT INTO ironwood_received_note_spends (ironwood_received_note_id, transaction_id)
             VALUES (1, 2);
             INSERT INTO ironwood_received_notes
             (transaction_id, action_index, account_id, diversifier, value,
              rho, rseed, is_change, memo, recipient_key_scope, note_version)
             SELECT 2, 0, account_id, diversifier, 10000,
                    rho, rseed, 1, X'F6', 0, note_version
             FROM ironwood_received_notes WHERE id = 1;",
            )
            .unwrap();
        if let Some(pool) = other_pool {
            let (table, column) = match pool {
                Pool::Orchard => ("orchard_received_note_spends", "orchard_received_note_id"),
                Pool::Sapling => ("sapling_received_note_spends", "sapling_received_note_id"),
                _ => unreachable!(),
            };
            fixture
                .conn
                .execute(
                    &format!("INSERT INTO {table} ({column}, transaction_id) VALUES (1, 2)"),
                    [],
                )
                .unwrap();
        }

        for txs in [fixture.transactions(), fixture.incoming()] {
            assert_eq!(txs.len(), 2);
            assert_eq!(txs[1].mined_height, Some(101));
            assert!(txs[1].incoming.is_empty());
            assert_eq!(txs[1].change.len(), 1);
            assert_eq!(txs[1].change[0].pool, Pool::Ironwood);
            assert_eq!(txs[1].change[0].recipient, fixture.address);
        }
    }

    #[test]
    fn ironwood_queries_reconstruct_and_filter_both_recipient_scopes() {
        let mut fixture = QueryFixture::new();
        fixture.diversifier = fixture.receiver(Pool::Sapling, Scope::External).1;
        let pools = [Pool::Sapling, Pool::Orchard, Pool::Ironwood];
        for pool in pools {
            fixture.add_scoped_note(pool, false, Scope::External, 0);
            fixture.add_scoped_note(pool, false, Scope::Internal, 1);
            let (external, external_diversifier) = fixture.receiver(pool, Scope::External);
            let (internal, internal_diversifier) = fixture.receiver(pool, Scope::Internal);
            assert_ne!(external, internal);
            assert_eq!(external_diversifier, internal_diversifier);
            assert_eq!(external_diversifier, fixture.diversifier);
        }

        let txs = fixture.transactions();
        assert_eq!(txs.len(), 1);
        assert_eq!(txs[0].incoming.len(), 6);
        for pool in pools {
            for scope in [Scope::External, Scope::Internal] {
                let (address, _) = fixture.receiver(pool, scope);
                assert_eq!(
                    txs[0]
                        .incoming
                        .iter()
                        .filter(|note| note.pool == pool && note.recipient == address)
                        .count(),
                    1
                );
                let incoming = get_incoming_payments(
                    &mut fixture.db,
                    &mut fixture.conn,
                    &Network::TestNetwork,
                    &address,
                    None,
                )
                .unwrap();
                assert_eq!(incoming.len(), 1);
                assert_eq!(
                    incoming[0].incoming.len(),
                    if pool == Pool::Sapling { 1 } else { 2 }
                );
                assert!(
                    incoming[0]
                        .incoming
                        .iter()
                        .all(|note| note.recipient == address)
                );
                assert!(
                    incoming[0]
                        .incoming
                        .iter()
                        .all(|note| if pool == Pool::Sapling {
                            note.pool == Pool::Sapling
                        } else {
                            matches!(note.pool, Pool::Orchard | Pool::Ironwood)
                        })
                );
            }
        }
    }

    #[test]
    fn ironwood_queries_reject_unknown_recipient_scopes() {
        let mut fixture = QueryFixture::new();
        fixture.add_note(Pool::Ironwood, false);
        for scope in [None, Some(-1), Some(2)] {
            fixture
                .conn
                .execute(
                    "UPDATE ironwood_received_notes SET recipient_key_scope = ?1",
                    [scope],
                )
                .unwrap();
            let error = crate::sync::get_transactions(
                &mut fixture.db,
                &mut fixture.conn,
                &Network::TestNetwork,
                Some(fixture.account_uuid),
                None,
                None,
            )
            .unwrap_err();
            assert!(error.to_string().contains("Unknown recipient key scope:"));
        }
    }

    #[test]
    fn ironwood_queries_track_unspent_notes_and_birthday_heights() {
        use crate::sql_statements::{get_birthday_heights_sql, get_unspent_notes_sql};

        let fixture = QueryFixture::new();
        let expiry = crate::analysis::expiry_delta_sql(&Network::TestNetwork);
        let notes_sql = get_unspent_notes_sql(&expiry);
        let birthday_sql = get_birthday_heights_sql(&expiry);
        fixture.add_note(Pool::Ironwood, false);
        fixture
            .conn
            .execute_batch("UPDATE ironwood_received_notes SET recipient_key_scope = 1;")
            .unwrap();
        let params = named_params! {
            ":account_uuid": fixture.account_uuid.expose_uuid(),
            ":target_height": 102,
        };
        let note: (u32, u64, u32, u32) = fixture
            .conn
            .query_row(&notes_sql, params, |row| {
                Ok((
                    row.get("block")?,
                    row.get("value")?,
                    row.get("output_pool")?,
                    row.get("recipient_key_scope")?,
                ))
            })
            .unwrap();
        assert_eq!(note, (100, 20000, 4, 1));
        let birthday: Option<u32> = fixture
            .conn
            .query_row(&birthday_sql, params, |row| {
                row.get("Block with first unspent note")
            })
            .unwrap();
        assert_eq!(birthday, Some(100));

        fixture
            .conn
            .execute_batch(
                "INSERT INTO transactions (id_tx, txid, mined_height, min_observed_height)
             VALUES (2, randomblob(32), 101, 101);
             INSERT INTO ironwood_received_note_spends (ironwood_received_note_id, transaction_id)
             VALUES (1, 2);",
            )
            .unwrap();
        let mut stmt = fixture.conn.prepare(&notes_sql).unwrap();
        assert!(stmt.query(params).unwrap().next().unwrap().is_none());
        let birthday: Option<u32> = fixture
            .conn
            .query_row(&birthday_sql, params, |row| {
                row.get("Block with first unspent note")
            })
            .unwrap();
        assert_eq!(birthday, None);
    }

    #[test]
    fn ironwood_queries_birthday_heights_avoid_mixed_pool_cross_products() {
        let fixture = QueryFixture::new();
        for pool in [Pool::Sapling, Pool::Orchard, Pool::Ironwood] {
            for index in 0..50 {
                fixture.add_scoped_note(pool, false, Scope::External, index);
            }
        }
        let sql = crate::sql_statements::get_birthday_heights_sql(
            &crate::analysis::expiry_delta_sql(&Network::TestNetwork),
        );
        let mut stmt = fixture.conn.prepare(&sql).unwrap();
        let rebirth: Option<u32> = stmt
            .query_row(
                named_params! {
                    ":account_uuid": fixture.account_uuid.expose_uuid(),
                    ":target_height": 150,
                },
                |row| row.get("Block with first unspent note"),
            )
            .unwrap();
        assert_eq!(rebirth, Some(100));
        let steps = stmt.get_status(rusqlite::StatementStatus::VmStep);
        assert!(
            steps > 0 && steps < 100_000,
            "150 notes should not form a cross product: {steps} SQLite VM steps"
        );
    }

    #[test]
    fn ironwood_queries_include_recovered_outputs_in_minimum_fees() {
        let fixture = QueryFixture::new_with_spending_keys(true);
        fixture.add_scoped_note(Pool::Ironwood, false, Scope::External, 0);
        fixture.add_scoped_note(Pool::Ironwood, false, Scope::External, 1);
        fixture.conn.execute_batch(
            "DELETE FROM scan_queue;
             INSERT INTO scan_queue (block_range_start, block_range_end, priority) VALUES (1, 150, 10);
             INSERT INTO sapling_tree_checkpoints (checkpoint_id) VALUES (100);
             INSERT INTO orchard_tree_checkpoints (checkpoint_id) VALUES (100);
             INSERT INTO transactions (id_tx, txid, expiry_height, min_observed_height)
             VALUES (2, randomblob(32), 149, 100);
             INSERT INTO ironwood_received_note_spends (ironwood_received_note_id, transaction_id) VALUES (1, 2), (2, 2);"
        ).unwrap();
        let config = crate::interop::DbInit {
            data_file: fixture.db.path.to_str().unwrap().to_owned(),
            network: Network::TestNetwork.into(),
        };
        let fee: u64 = zcash_primitives::transaction::fees::zip317::FeeRule::standard()
            .marginal_fee()
            .into();
        let balances = crate::analysis::get_user_balances(
            &config,
            &fixture.account_uuid,
            zcash_client_backend::data_api::wallet::ConfirmationsPolicy::default(),
        )
        .unwrap();
        assert_eq!(balances.minimum_fees, 3 * fee);
        fixture
            .conn
            .execute(
                "UPDATE transactions SET expiry_height = 150 WHERE id_tx = 2",
                [],
            )
            .unwrap();
        let balances = crate::analysis::get_user_balances(
            &config,
            &fixture.account_uuid,
            zcash_client_backend::data_api::wallet::ConfirmationsPolicy::default(),
        )
        .unwrap();
        assert_eq!(balances.minimum_fees, 0);
    }

    #[test]
    fn ironwood_queries_recover_notes_after_expired_spends() {
        let fixture = QueryFixture::new();
        for pool in [Pool::Sapling, Pool::Orchard, Pool::Ironwood] {
            fixture.add_note(pool, false);
        }
        fixture.conn.execute_batch(
            "INSERT INTO transactions (id_tx, txid, expiry_height, min_observed_height)
             VALUES (2, randomblob(32), 105, 100);
             INSERT INTO sapling_received_note_spends (sapling_received_note_id, transaction_id) VALUES (1, 2);
             INSERT INTO orchard_received_note_spends (orchard_received_note_id, transaction_id) VALUES (1, 2);
             INSERT INTO ironwood_received_note_spends (ironwood_received_note_id, transaction_id) VALUES (1, 2);"
        ).unwrap();

        assert_notes_available(&fixture, 105, false);
        assert_notes_available(&fixture, 106, true);
        fixture
            .conn
            .execute(
                "UPDATE transactions SET mined_height = 104 WHERE id_tx = 2",
                [],
            )
            .unwrap();
        assert_notes_available(&fixture, 106, false);
        fixture
            .conn
            .execute(
                "UPDATE transactions SET mined_height = NULL, expiry_height = 0 WHERE id_tx = 2",
                [],
            )
            .unwrap();
        assert_notes_available(&fixture, 200, false);
        fixture
            .conn
            .execute(
                "UPDATE transactions SET expiry_height = NULL WHERE id_tx = 2",
                [],
            )
            .unwrap();
        let expiry = zcash_primitives::transaction::builder::default_tx_expiry_delta(
            zcash_protocol::consensus::BranchId::for_height(
                &Network::TestNetwork,
                BlockHeight::from_u32(100),
            ),
        );
        assert_notes_available(&fixture, 100 + expiry, false);
        assert_notes_available(&fixture, 101 + expiry, true);

        fixture.conn.execute_batch(
            "INSERT INTO transactions (id_tx, txid, expiry_height, min_observed_height)
             VALUES (3, randomblob(32), 200, 100);
             INSERT INTO sapling_received_note_spends (sapling_received_note_id, transaction_id) VALUES (1, 3);
             INSERT INTO orchard_received_note_spends (orchard_received_note_id, transaction_id) VALUES (1, 3);
             INSERT INTO ironwood_received_note_spends (ironwood_received_note_id, transaction_id) VALUES (1, 3);"
        ).unwrap();
        assert_notes_available(&fixture, 150, false);
        assert_notes_available(&fixture, 201, true);

        for height in zcash_protocol::consensus::UPGRADES_IN_ORDER
            .iter()
            .filter_map(|upgrade| Network::TestNetwork.activation_height(*upgrade))
        {
            fixture.conn.execute(
                "UPDATE transactions SET expiry_height = NULL, min_observed_height = ?1 WHERE id_tx = 2",
                [u32::from(height)],
            ).unwrap();
            let expiry = zcash_primitives::transaction::builder::default_tx_expiry_delta(
                zcash_protocol::consensus::BranchId::for_height(&Network::TestNetwork, height),
            );
            assert_notes_available(&fixture, u32::from(height) + expiry, false);
            assert_notes_available(&fixture, u32::from(height) + expiry + 1, true);
        }
    }

    fn assert_notes_available(fixture: &QueryFixture, target_height: u32, available: bool) {
        fixture.conn.execute("DELETE FROM scan_queue", []).unwrap();
        fixture.conn.execute(
            "INSERT INTO scan_queue (block_range_start, block_range_end, priority) VALUES (1, ?1, 10)",
            [target_height],
        ).unwrap();
        let sql = crate::sql_statements::get_unspent_notes_sql(&crate::analysis::expiry_delta_sql(
            &Network::TestNetwork,
        ));
        let mut stmt = fixture.conn.prepare(&sql).unwrap();
        let mut notes = stmt
            .query_map(
                named_params! {
                    ":account_uuid": fixture.account_uuid.expose_uuid(),
                    ":target_height": target_height,
                },
                |row| {
                    Ok((
                        row.get::<_, u32>("output_pool")?,
                        row.get::<_, u64>("value")?,
                    ))
                },
            )
            .unwrap()
            .collect::<Result<Vec<_>, _>>()
            .unwrap();
        notes.sort();
        assert_eq!(
            notes,
            if available {
                vec![(2, 20_000), (3, 20_000), (4, 20_000)]
            } else {
                vec![]
            }
        );
        let heights = crate::analysis::get_birthday_heights(
            crate::interop::DbInit {
                data_file: fixture.db.path.to_str().unwrap().to_owned(),
                network: Network::TestNetwork.into(),
            },
            &fixture.account_uuid,
        )
        .unwrap();
        assert_eq!(heights.birthday_height, Some(100));
        assert_eq!(
            heights.rebirth_height,
            if available { Some(100) } else { None }
        );
    }

    #[test]
    fn ironwood_queries_still_reject_unknown_pool_codes() {
        let mut fixture = QueryFixture::new();
        fixture.add_note(Pool::Ironwood, false);
        fixture
            .conn
            .execute_batch(
                "CREATE TEMP VIEW v_tx_outputs AS
             SELECT txid, 5 AS output_pool, output_index, from_account_uuid,
                    to_account_uuid, to_address, value, memo
             FROM main.v_tx_outputs;",
            )
            .unwrap();
        let error = crate::sync::get_transactions(
            &mut fixture.db,
            &mut fixture.conn,
            &Network::TestNetwork,
            Some(fixture.account_uuid),
            None,
            None,
        )
        .unwrap_err();
        assert!(error.to_string().contains("Unknown output pool type: 5"));
    }
}
