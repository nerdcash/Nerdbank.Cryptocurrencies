use std::time::SystemTime;

use http::{uri::InvalidUri, Uri};
use rusqlite::{named_params, Connection};
use tokio::runtime::Runtime;
use zcash_address::TryFromRawAddress;
use zcash_client_backend::{
    address::{RecipientAddress, UnifiedAddress},
    data_api::WalletRead,
};
use zcash_client_sqlite::error::SqliteClientError;
use zcash_primitives::{consensus::Network, zip32::AccountId};

use crate::{
    backend_client::sync, backing_store::Db, error::Error, grpc::destroy_channel,
    lightclient::get_block_height, sql_statements::GET_TRANSACTIONS_SQL,
};

lazy_static! {
    static ref RT: Runtime = tokio::runtime::Runtime::new().unwrap();
}

pub enum ChainType {
    Mainnet,
    Testnet,
}

impl From<ChainType> for Network {
    fn from(chain_type: ChainType) -> Self {
        match chain_type {
            ChainType::Mainnet => Network::MainNetwork,
            ChainType::Testnet => Network::TestNetwork,
        }
    }
}

pub struct WalletInfo {
    pub ufvk: Option<String>,
    pub unified_spending_key: Option<Vec<u8>>,
    pub birthday_height: u64,
}

#[derive(Debug)]
pub struct Transaction {
    pub txid: Vec<u8>,
    pub block_time: SystemTime,
    pub mined_height: Option<u32>,
    pub expired_unmined: bool,
    pub account_balance_delta: u64,
    pub fee: u64,
    pub outgoing: Vec<TransactionSendDetail>,
    pub incoming_transparent: Vec<TransparentNote>,
    pub incoming_shielded: Vec<ShieldedNote>,
}

#[derive(Debug)]
pub struct TransparentNote {
    pub value: u64,
    pub recipient: String,
}

#[derive(Debug)]
pub struct ShieldedNote {
    pub recipient: String,
    pub value: u64,
    pub memo: Vec<u8>,
    pub is_change: bool,
}

#[derive(Debug)]
pub struct TransactionSendDetail {
    pub recipient: String,
    pub value: u64,
    pub memo: Vec<u8>,
}

#[derive(Debug, thiserror::Error)]
pub enum LightWalletError {
    #[error("Invalid URI")]
    InvalidUri,

    #[error("Sqlite client error")]
    SqliteClientError(SqliteClientError),

    #[error("{message}")]
    Other { message: String },
}

impl From<InvalidUri> for LightWalletError {
    fn from(_: InvalidUri) -> Self {
        LightWalletError::InvalidUri
    }
}

impl From<SqliteClientError> for LightWalletError {
    fn from(e: SqliteClientError) -> Self {
        LightWalletError::SqliteClientError(e)
    }
}

impl From<rusqlite::Error> for LightWalletError {
    fn from(e: rusqlite::Error) -> Self {
        LightWalletError::SqliteClientError(SqliteClientError::from(e))
    }
}

impl From<time::error::ComponentRange> for LightWalletError {
    fn from(e: time::error::ComponentRange) -> Self {
        LightWalletError::Other {
            message: format!("Invalid time: {}", e),
        }
    }
}

impl From<Error> for LightWalletError {
    fn from(e: Error) -> Self {
        match e {
            Error::InternalError(msg) => LightWalletError::Other { message: msg },
            _ => LightWalletError::Other {
                message: "Unknown error".to_string(),
            },
        }
    }
}

pub struct BirthdayHeights {
    /// The original birthday height given at account creation if non-zero,
    /// otherwise the block number of the first transaction if any,
    /// otherwise the sapling activation height.
    pub original_birthday_height: u64,
    /// The block number of the first transaction if any,
    /// otherwise the sapling activation height.
    pub birthday_height: u64,
    /// The block number of the oldest unspent note or UTXO, if any.
    pub rebirth_height: Option<u64>,
}

/// Balances that may be presented to a user in a wallet app.
/// The goal is to present a user-friendly and useful view of what the user has or can soon expect
/// *without* requiring the user to understand the details of the Zcash protocol.
///
/// Showing all these balances all the time may overwhelm the user with information.
/// A simpler view may present an overall balance as:
///
/// Name | Value
/// --- | ---
/// "Balance" | `spendable` - `minimum_fees` + `immature_change` + `immature_income`
/// "Incoming" | `incoming`
///
/// If dust is sent to the wallet, the simpler view's Incoming balance would include it,
/// only for it to evaporate when confirmed.
/// But incoming can always evaporate (e.g. a transaction expires before confirmation),
/// and the alternatives being to either hide that a transmission was made at all, or to include
/// the dust in other balances could be more misleading.
///
/// An app *could* choose to prominently warn the user if a significant proportion of the incoming balance is dust,
/// although this event seems very unlikely since it will cost the sender *more* than the amount the recipient is expecting
/// to 'fool' them into thinking they are receiving value.
/// The more likely scenario is that the sender is trying to send a small amount of value as a new user and doesn't realize
/// the value is too small to be useful.
/// A good Zcash wallet should prevent sending dust in the first place.
pub struct UserBalances {
    /// Available for immediate spending.
    /// Expected fees are *not* deducted from this value, but the app may do so by subtracting `minimum_fees`.
    /// `dust` is excluded from this value.
    ///
    /// For enhanced privacy, the minimum number of required confirmations to spend a note is usually greater than one.
    pub spendable: u64,

    /// The sum of the change notes that have insufficient confirmations to be spent.
    pub immature_change: u64,

    /// The minimum fees that can be expected to spend all `spendable + immature_change` funds in the wallet.
    /// This fee assumes all funds will be sent to a single note.
    ///
    /// Balances described by other fields in this struct are not included because they are not confirmed,
    /// they may amount to dust, or because as `immature_income` funds they may require shielding which has a cost
    /// and can change the amount of fees required to spend them (e.g. 3 UTXOs shielded together become only 1 note).
    pub minimum_fees: u64,

    /// The sum of non-change notes with a non-zero confirmation count that is less than the minimum required for spending,
    /// and all UTXOs (considering that UTXOs must be shielded before spending).
    /// `dust` is excluded from this value.
    ///
    /// As funds mature, this may not be the exact amount added to `spendable`, since the process of maturing
    /// may require shielding, which has a cost.
    pub immature_income: u64,

    /// The sum of all *confirmed* UTXOs and notes that are worth less than the fee to spend them,
    /// making them essentially inaccessible.
    pub dust: u64,

    /// The sum of all *unconfirmed* UTXOs and notes that are not change.
    /// This value includes any applicable `incoming_dust`.
    pub incoming: u64,

    /// The sum of all *unconfirmed* UTXOs and notes that are not change and are each counted as dust.
    pub incoming_dust: u64,
}

#[derive(Debug, Clone)]
pub struct SyncResult {
    pub tip_height: u64,
}

pub struct DbInit {
    pub data_file: String,
    pub network: ChainType,
}

pub fn lightwallet_init(config: DbInit) -> Result<(), LightWalletError> {
    RT.block_on(async move {
        Db::init(config.data_file, config.network.into())?;
        Ok(())
    })
}

pub fn lightwallet_get_birthday_height(config: DbInit) -> Result<Option<u32>, LightWalletError> {
    RT.block_on(async move {
        let db = Db::load(config.data_file, config.network.into())?;
        Ok(db.data.get_wallet_birthday()?.map(|h| h.into()))
    })
}

pub fn lightwallet_get_block_height(uri: String) -> Result<u32, LightWalletError> {
    let uri: Uri = uri.parse()?;
    RT.block_on(async move { Ok(get_block_height(uri).await?) })
}

pub fn lightwallet_get_sync_height(config: DbInit) -> Result<Option<u32>, LightWalletError> {
    RT.block_on(async move {
        let db = Db::load(config.data_file, config.network.into())?;
        Ok(db.data.get_max_height_hash()?.map(|h| h.0.into()))
    })
}

pub fn lightwallet_sync(config: DbInit, uri: String) -> Result<SyncResult, LightWalletError> {
    let uri: Uri = uri.parse()?;
    RT.block_on(async move { Ok(sync(uri, config.data_file).await?) })
}

pub fn lightwallet_get_transactions(
    config: DbInit,
    account_id: u32,
    starting_block: u32,
) -> Result<Vec<Transaction>, LightWalletError> {
    RT.block_on(async move {
        let network: Network = config.network.into();
        let db = Db::load(config.data_file.clone(), network)?;
        let ufvkeys = db.data.get_unified_full_viewing_keys()?;
        let ufvk = ufvkeys.get(&AccountId::from(account_id));

        let conn = Connection::open(config.data_file)?;
        rusqlite::vtab::array::load_module(&conn)?;

        let mut stmt_txs = conn.prepare(GET_TRANSACTIONS_SQL)?;

        let rows = stmt_txs.query_and_then(
            named_params! {
                ":account_id": account_id,
                ":starting_block": starting_block,
            },
            |row| -> Result<Transaction, LightWalletError> {
                let output_pool: u32 = row.get("output_pool")?;
                let from_account: Option<u32> = row.get("from_account")?;
                let to_account: Option<u32> = row.get("to_account")?;
                let mut recipient: Option<String> = row.get("to_address")?;
                let value: u64 = row.get("value")?;
                let memo: Option<Vec<u8>> = row.get("memo")?;
                let memo = memo.unwrap_or(Vec::new());

                // Work out the receiving address when the sqlite db doesn't record it
                // but we have a diversifier that can regenerate it.
                if recipient.is_none() {
                    let diversifier: Option<Vec<u8>> = row.get("diversifier")?;
                    if let Some(diversifier) = diversifier {
                        recipient = match output_pool {
                            2 => ufvk
                                .map(|k| {
                                    k.sapling().map(|s| {
                                        s.diversified_address(
                                            zcash_primitives::sapling::Diversifier(
                                                diversifier.try_into().unwrap(),
                                            ),
                                        )
                                        .map(|a| {
                                            RecipientAddress::try_from_raw_sapling(a.to_bytes())
                                                .unwrap()
                                                .encode(&network)
                                        })
                                    }).flatten()
                                })
                                .flatten(),
                            3 => ufvk
                                .map(|k| {
                                    k.orchard().map(|o| {
                                        RecipientAddress::from(
                                            UnifiedAddress::from_receivers(
                                                Some(o.address(
                                                    orchard::keys::Diversifier::from_bytes(
                                                        diversifier.try_into().unwrap(),
                                                    ),
                                                    orchard::keys::Scope::External,
                                                )),
                                                None,
                                                None,
                                            )
                                            .unwrap(),
                                        )
                                        .encode(&network)
                                    })
                                })
                                .flatten(),
                            _ => None,
                        }
                    }
                }

                let mut tx = Transaction {
                    txid: row.get::<_, Vec<u8>>("txid")?,
                    mined_height: row.get("mined_height")?,
                    expired_unmined: row.get("expired_unmined")?,
                    block_time: time::OffsetDateTime::from_unix_timestamp(
                        row.get::<_, i64>("block_time")?,
                    )?
                    .into(),
                    fee: row.get::<_, u64>("fee_paid")?,
                    account_balance_delta: row.get("account_balance_delta")?,
                    incoming_transparent: Vec::new(),
                    incoming_shielded: Vec::new(),
                    outgoing: Vec::new(),
                };

                if to_account == Some(account_id) {
                    match output_pool {
                        0 => tx.incoming_transparent.push(TransparentNote {
                            value,
                            recipient: recipient.clone().unwrap(),
                        }),
                        1..=3 => tx.incoming_shielded.push(ShieldedNote {
                            value,
                            memo: memo.clone(),
                            recipient: recipient.clone().unwrap(), // TODO: this will fail because recipient is NULL. Reconstruct from diversifier.
                            is_change: false,
                        }),
                        _ => {
                            return Err(LightWalletError::Other {
                                message: format!("Unsupported output pool value {}.", output_pool),
                            });
                        }
                    }
                };

                if from_account == Some(account_id) && recipient.is_some() {
                    tx.outgoing.push(TransactionSendDetail {
                        recipient: recipient.unwrap(),
                        memo,
                        value,
                    });
                }

                Ok(tx)
            },
        )?;

        let mut result: Vec<Transaction> = Vec::new();
        for row_result in rows {
            let mut row = row_result?;

            let last = result.last();
            let add = last.is_some() && last.unwrap().txid.eq(&row.txid);
            if add {
                // This row adds line items to the last transaction.
                // Pop it off the list to change it, then we'll add it back.
                let mut tx = result.pop().unwrap();
                tx.incoming_transparent
                    .append(&mut row.incoming_transparent);
                tx.incoming_shielded.append(&mut row.incoming_shielded);
                tx.outgoing.append(&mut row.outgoing);
                result.push(tx);
            } else {
                result.push(row);
            }
        }

        Ok(result)
    })
}

pub fn lightwallet_disconnect_server(uri: String) -> Result<bool, LightWalletError> {
    let uri: Uri = uri.parse()?;
    RT.block_on(async move { Ok(destroy_channel(uri)) })
}
