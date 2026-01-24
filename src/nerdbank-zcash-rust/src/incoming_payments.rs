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
use zip32::DiversifierIndex;

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
    /// The diversifier bytes for shielded receivers (Orchard/Sapling).
    /// For transparent, this will be None since transparent uses address strings.
    diversifier: Option<Vec<u8>>,
    /// For transparent addresses, the encoded address string.
    transparent_address: Option<String>,
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
                        recipient = match output_pool {
                            PoolType::SAPLING => ufvk.and_then(|k| {
                                k.sapling().and_then(|s| {
                                    s.diversified_address(sapling::keys::Diversifier(
                                        diversifier.clone().try_into().unwrap(),
                                    ))
                                    .map(|a| a.encode(network))
                                })
                            }),
                            PoolType::ORCHARD => ufvk.and_then(|k| {
                                k.orchard().map(|o| {
                                    UnifiedAddress::from_receivers(
                                        Some(o.address(
                                            orchard::keys::Diversifier::from_bytes(
                                                diversifier.clone().try_into().unwrap(),
                                            ),
                                            Scope::External,
                                        )),
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
                for note in row.incoming {
                    if !existing
                        .incoming
                        .iter()
                        .any(|n| n.value == note.value && n.recipient == note.recipient)
                    {
                        existing.incoming.push(note);
                    }
                }
                for note in row.change {
                    if !existing
                        .change
                        .iter()
                        .any(|n| n.value == note.value && n.recipient == note.recipient)
                    {
                        existing.change.push(note);
                    }
                }
            } else {
                all_transactions.push(row);
            }
        }
    }

    // Sort by mined height
    all_transactions.sort_by(|a, b| a.mined_height.cmp(&b.mined_height));

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
            if let Some(orchard_addr) = ua.orchard() {
                if let Some((account_uuid, diversifier)) =
                    find_orchard_receiver_info(&ufvkeys, orchard_addr)
                {
                    receiver_infos.push(ReceiverInfo {
                        account_uuid,
                        diversifier: Some(diversifier),
                        transparent_address: None,
                    });
                }
            }

            if let Some(sapling_addr) = ua.sapling() {
                if let Some((account_uuid, diversifier)) =
                    find_sapling_receiver_info(&ufvkeys, sapling_addr)
                {
                    receiver_infos.push(ReceiverInfo {
                        account_uuid,
                        diversifier: Some(diversifier),
                        transparent_address: None,
                    });
                }
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
                    });
                }
            }
        }
        zcash_keys::address::Address::Sapling(pa) => {
            if let Some((account_uuid, diversifier)) = find_sapling_receiver_info(&ufvkeys, &pa) {
                receiver_infos.push(ReceiverInfo {
                    account_uuid,
                    diversifier: Some(diversifier),
                    transparent_address: None,
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
                });
            }
        }
    }

    if receiver_infos.is_empty() {
        return Err(Error::InvalidAddress);
    }

    Ok(receiver_infos)
}

/// Find the account and diversifier for an Orchard receiver.
fn find_orchard_receiver_info(
    ufvkeys: &std::collections::HashMap<AccountUuid, zcash_keys::keys::UnifiedFullViewingKey>,
    orchard_addr: &orchard::Address,
) -> Option<(AccountUuid, Vec<u8>)> {
    for (account_uuid, ufvk) in ufvkeys {
        if let Some(fvk) = ufvk.orchard() {
            let ivk = fvk.to_ivk(Scope::External);
            // Check if this IVK owns this address by trying to get the diversifier index
            if ivk.diversifier_index(orchard_addr).is_some() {
                // The diversifier stored in the DB is the diversifier from the address itself
                let diversifier = orchard_addr.diversifier();
                return Some((*account_uuid, diversifier.as_array().to_vec()));
            }
        }
    }
    None
}

/// Find the account and diversifier for a Sapling receiver.
fn find_sapling_receiver_info(
    ufvkeys: &std::collections::HashMap<AccountUuid, zcash_keys::keys::UnifiedFullViewingKey>,
    sapling_addr: &sapling::PaymentAddress,
) -> Option<(AccountUuid, Vec<u8>)> {
    for (account_uuid, ufvk) in ufvkeys {
        if let Some(dfvk) = ufvk.sapling() {
            let ivk = dfvk.to_external_ivk();
            // Check if this IVK can decrypt to this address's diversifier
            // The diversifier is directly available from the payment address
            let diversifier = sapling_addr.diversifier();
            // Verify this address belongs to this account by checking if the IVK
            // can derive the same address from this diversifier
            let diversifier_bytes: [u8; 11] = diversifier.0;
            if let Some(derived_addr) = ivk.address_at(DiversifierIndex::from(diversifier_bytes)) {
                if &derived_addr == sapling_addr {
                    return Some((*account_uuid, diversifier.0.to_vec()));
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
    // Tests would go here but require a test database setup
}
