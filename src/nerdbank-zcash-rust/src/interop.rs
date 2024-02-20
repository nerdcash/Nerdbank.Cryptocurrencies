use std::{
    collections::HashMap,
    num::NonZeroU32,
    sync::{
        atomic::{AtomicU32, Ordering},
        Mutex,
    },
    time::SystemTime,
};

use http::{uri::InvalidUri, Uri};
use orchard::keys::Scope;
use rusqlite::{named_params, Connection};
use secrecy::SecretVec;
use tokio::runtime::Runtime;
use tokio_util::sync::CancellationToken;
use zcash_client_backend::{
    address::UnifiedAddress,
    data_api::WalletRead,
    encoding::AddressCodec,
    keys::{Era, UnifiedSpendingKey},
};
use zcash_client_sqlite::error::SqliteClientError;
use zcash_primitives::{
    consensus::Network,
    legacy::TransparentAddress,
    zip32::{AccountId, DiversifierIndex},
};

use crate::{
    analysis::{BirthdayHeights, UserBalances},
    backing_store::Db,
    error::Error,
    grpc::{destroy_channel, get_client},
    send::send_transaction,
    shield::shield_funds_at_address,
    sql_statements::GET_TRANSACTIONS_SQL,
};

lazy_static! {
    static ref RT: Runtime = tokio::runtime::Runtime::new().unwrap();
}

pub trait SyncUpdate: Send + Sync + std::fmt::Debug {
    fn update_status(&self, data: SyncUpdateData);
    fn report_transactions(&self, transactions: Vec<Transaction>);
}

pub trait CancellationSource: Send + Sync + std::fmt::Debug {
    fn set_cancellation_id(&self, id: u32);
}

impl From<uniffi::UnexpectedUniFFICallbackError> for LightWalletError {
    fn from(e: uniffi::UnexpectedUniFFICallbackError) -> Self {
        LightWalletError::Other {
            message: e.to_string(),
        }
    }
}

#[derive(Debug, Clone)]
pub struct SyncUpdateData {
    pub last_fully_scanned_block: Option<u32>,
    pub tip_height: u32,
    pub current_step: u64,
    pub total_steps: u64,
    pub last_error: Option<String>,
}

#[derive(Debug, Copy, Clone)]
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

impl From<Network> for ChainType {
    fn from(network: Network) -> Self {
        match network {
            Network::MainNetwork => ChainType::Mainnet,
            Network::TestNetwork => ChainType::Testnet,
        }
    }
}

pub struct AccountInfo {
    pub id: u32,
    pub uvk: Option<String>,
    pub birthday_heights: BirthdayHeights,
}

#[derive(Debug, Clone)]
pub struct Transaction {
    pub txid: Vec<u8>,
    pub block_time: Option<SystemTime>,
    pub mined_height: Option<u32>,
    pub expired_unmined: bool,
    pub account_balance_delta: i64,
    pub fee: u64,
    pub outgoing: Vec<TransactionSendDetail>,
    pub incoming_transparent: Vec<TransparentNote>,
    pub incoming_shielded: Vec<ShieldedNote>,
}

#[derive(Debug, Clone)]
pub struct TransparentNote {
    pub value: u64,
    pub recipient: String,
}

#[derive(Debug, Clone)]
pub struct ShieldedNote {
    pub recipient: String,
    pub value: u64,
    pub memo: Vec<u8>,
    pub is_change: bool,
}

#[derive(Debug, Clone)]
pub struct TransactionSendDetail {
    pub recipient: String,
    pub value: u64,
    pub memo: Option<Vec<u8>>,
}

#[derive(Debug, thiserror::Error)]
pub enum LightWalletError {
    #[error("Invalid argument: {message}")]
    InvalidArgument { message: String },

    #[error("Invalid URI")]
    InvalidUri,

    #[error("Sqlite client error: {message}")]
    SqliteClientError { message: String },

    #[error("The operation was canceled.")]
    Canceled,

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
        LightWalletError::SqliteClientError {
            message: e.to_string(),
        }
    }
}

impl From<rusqlite::Error> for LightWalletError {
    fn from(e: rusqlite::Error) -> Self {
        LightWalletError::SqliteClientError {
            message: e.to_string(),
        }
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
            Error::TonicStatus(status) if status.code() == tonic::Code::Cancelled => {
                LightWalletError::Canceled
            }
            Error::Internal(msg) => LightWalletError::Other { message: msg },
            _ => LightWalletError::Other {
                message: e.to_string(),
            },
        }
    }
}

#[derive(Debug, Clone)]
pub struct DbInit {
    pub data_file: String,
    pub network: ChainType,
}

lazy_static! {
    static ref CANCELLATION_TOKENS: Mutex<HashMap<u32, CancellationToken>> =
        Mutex::new(HashMap::new());
    static ref TOKEN_COUNTER: AtomicU32 = AtomicU32::new(1);
}

struct InteropCancellationToken(CancellationToken, Option<u32>);

impl Drop for InteropCancellationToken {
    fn drop(&mut self) {
        if let Some(id) = self.1 {
            let mut tokens = CANCELLATION_TOKENS.lock().unwrap();
            tokens.remove(&id);
        }
    }
}

fn get_cancellation_token(
    client: Option<Box<dyn CancellationSource>>,
) -> Result<InteropCancellationToken, LightWalletError> {
    match client {
        Some(source) => {
            let (handle, token) = {
                let mut tokens = CANCELLATION_TOKENS.lock().unwrap();
                let handle = TOKEN_COUNTER.fetch_add(1, Ordering::SeqCst);
                let token = CancellationToken::new();
                tokens.insert(handle, token.clone());
                (handle, token)
            };

            // Notify the client of the ID that was assigned so they can call cancel(u32) with it later.
            source.set_cancellation_id(handle);

            Ok(InteropCancellationToken(token, Some(handle)))
        }
        None => Ok(InteropCancellationToken(CancellationToken::new(), None)),
    }
}

pub fn cancel(id: u32) -> Result<(), LightWalletError> {
    let mut tokens = CANCELLATION_TOKENS.lock().unwrap();
    if let Some(token) = tokens.remove(&id) {
        token.cancel();
    }

    Ok(())
}

pub fn init(config: DbInit) -> Result<(), LightWalletError> {
    RT.block_on(async move {
        Db::init(config.data_file, config.network.into())?;
        Ok(())
    })
}

pub fn add_account(
    config: DbInit,
    uri: String,
    seed: Vec<u8>,
    birthday_height: Option<u32>,
    cancellation: Option<Box<dyn CancellationSource>>,
) -> Result<u32, LightWalletError> {
    use crate::lightclient::get_block_height;
    let cancellation_token = get_cancellation_token(cancellation)?;

    RT.block_on(async move {
        let mut db = Db::load(config.data_file, config.network.into())?;
        let mut client = get_client(uri.parse()?).await.map_err(Error::from)?;
        let birthday_height = match birthday_height {
            Some(v) => v,
            None => get_block_height(uri.parse()?, cancellation_token.0.clone()).await?,
        };
        let secret = SecretVec::new(seed);
        let account = db
            .add_account(&secret, birthday_height as u64, &mut client)
            .await?;
        Ok(account.0.into())
    })
}

pub fn get_accounts(config: DbInit) -> Result<Vec<AccountInfo>, LightWalletError> {
    use crate::analysis::get_birthday_heights;

    let db = Db::load(config.data_file.clone(), config.network.into())?;
    let network: Network = config.network.into();
    let mut result = Vec::new();
    for account_info in db.data.get_unified_full_viewing_keys()?.iter() {
        result.push(AccountInfo {
            id: account_info.0.to_owned().into(),
            uvk: Some(account_info.1.encode(&network)),
            birthday_heights: get_birthday_heights(config.clone(), account_info.0.to_owned())?,
        });
    }

    Ok(result)
}

pub fn add_diversifier(
    config: DbInit,
    account: u32,
    diversifier_index: Vec<u8>,
) -> Result<String, LightWalletError> {
    RT.block_on(async move {
        let network = config.network.into();
        let mut db = Db::load(config.data_file, network)?;
        let diversified_index: [u8; 11] =
            diversifier_index
                .try_into()
                .map_err(|_| LightWalletError::InvalidArgument {
                    message: "Bad diversifier".to_string(),
                })?;
        let diversifier_index = DiversifierIndex::from(diversified_index);
        Ok(db
            .add_diversifier(
                account
                    .try_into()
                    .map_err(|_| LightWalletError::InvalidArgument {
                        message: "invalid account id".to_string(),
                    })?,
                diversifier_index,
            )?
            .encode(&network))
    })
}

pub fn get_birthday_height(config: DbInit) -> Result<Option<u32>, LightWalletError> {
    RT.block_on(async move {
        let db = Db::load(config.data_file, config.network.into())?;
        Ok(db.data.get_wallet_birthday()?.map(|h| h.into()))
    })
}

pub fn get_block_height(
    uri: String,
    cancellation: Option<Box<dyn CancellationSource>>,
) -> Result<u32, LightWalletError> {
    use crate::lightclient::get_block_height;
    let uri: Uri = uri.parse()?;
    let cancellation_token = get_cancellation_token(cancellation)?;
    RT.block_on(async move { Ok(get_block_height(uri, cancellation_token.0.clone()).await?) })
}

pub fn get_sync_height(config: DbInit) -> Result<Option<u32>, LightWalletError> {
    RT.block_on(async move {
        let db = Db::load(config.data_file, config.network.into())?;
        Ok(db.data.get_max_height_hash()?.map(|h| h.0.into()))
    })
}

pub fn sync(
    config: DbInit,
    uri: String,
    progress: Option<Box<dyn SyncUpdate>>,
    continually: bool,
    cancellation: Option<Box<dyn CancellationSource>>,
) -> Result<SyncUpdateData, LightWalletError> {
    use crate::sync::sync;
    let uri: Uri = uri.parse()?;
    let cancellation_token = get_cancellation_token(cancellation)?;
    RT.block_on(async move {
        Ok(sync(
            uri,
            config.data_file,
            progress,
            continually,
            cancellation_token.0.clone(),
        )
        .await?)
    })
}

pub fn get_transactions(
    config: DbInit,
    account_id: u32,
    starting_block: u32,
) -> Result<Vec<Transaction>, LightWalletError> {
    RT.block_on(async move {
        let network: Network = config.network.into();
        let db = Db::load(config.data_file.clone(), network)?;
        let ufvkeys = db.data.get_unified_full_viewing_keys()?;
        let ufvk = ufvkeys.get(
            &AccountId::try_from(account_id)
                .map_err(|_| Error::InvalidArgument("Invalid account ID".to_string()))?,
        );

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
                let memo = memo.unwrap_or_default();

                // Work out the receiving address when the sqlite db doesn't record it
                // but we have a diversifier that can regenerate it.
                if recipient.is_none() {
                    let diversifier: Option<Vec<u8>> = row.get("diversifier")?;
                    if let Some(diversifier) = diversifier {
                        recipient = match output_pool {
                            2 => ufvk.and_then(|k| {
                                k.sapling().and_then(|s| {
                                    s.diversified_address(sapling::keys::Diversifier(
                                        diversifier.try_into().unwrap(),
                                    ))
                                    .map(|a| a.encode(&network))
                                })
                            }),
                            3 => ufvk.and_then(|k| {
                                k.orchard().map(|o| {
                                    UnifiedAddress::from_receivers(
                                        Some(o.address(
                                            orchard::keys::Diversifier::from_bytes(
                                                diversifier.try_into().unwrap(),
                                            ),
                                            Scope::External,
                                        )),
                                        None,
                                        None,
                                    )
                                    .unwrap()
                                    .encode(&network)
                                })
                            }),
                            _ => None,
                        }
                    }
                }

                let mut tx = Transaction {
                    txid: row.get::<_, Vec<u8>>("txid")?,
                    mined_height: row.get("mined_height")?,
                    expired_unmined: row
                        .get::<_, Option<bool>>("expired_unmined")?
                        .unwrap_or(false),
                    block_time: match row.get::<_, Option<i64>>("block_time")? {
                        Some(v) => Some(time::OffsetDateTime::from_unix_timestamp(v)?.into()),
                        None => None,
                    },
                    fee: row.get::<_, Option<u64>>("fee_paid")?.unwrap_or(0),
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

                if let Some(recipient) = recipient {
                    if from_account == Some(account_id) {
                        tx.outgoing.push(TransactionSendDetail {
                            recipient,
                            memo: Some(memo),
                            value,
                        });
                    }
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

pub fn get_birthday_heights(
    config: DbInit,
    account_id: u32,
) -> Result<BirthdayHeights, LightWalletError> {
    use crate::analysis::get_birthday_heights;

    Ok(get_birthday_heights(
        config,
        account_id
            .try_into()
            .map_err(|_| Error::InvalidArgument("bad account id".to_string()))?,
    )?)
}

pub fn get_user_balances(
    config: DbInit,
    account_id: u32,
    min_confirmations: u32,
) -> Result<UserBalances, LightWalletError> {
    use crate::analysis::get_user_balances;
    Ok(get_user_balances(
        config,
        account_id
            .try_into()
            .map_err(|_| Error::InvalidArgument("Invalid account id".to_string()))?,
        NonZeroU32::try_from(min_confirmations)
            .map_err(|_| Error::InvalidArgument("A positive integer is required.".to_string()))?,
    )?)
}

pub fn disconnect_server(uri: String) -> Result<bool, LightWalletError> {
    let uri: Uri = uri.parse()?;
    RT.block_on(async move { Ok(destroy_channel(uri)) })
}

pub struct SendTransactionResult {
    pub txid: Vec<u8>,
}

pub fn send(
    config: DbInit,
    uri: String,
    usk: Vec<u8>,
    min_confirmations: u32,
    send_details: Vec<TransactionSendDetail>,
) -> Result<SendTransactionResult, LightWalletError> {
    let uri: Uri = uri.parse()?;
    let usk = UnifiedSpendingKey::from_bytes(Era::Orchard, &usk).map_err(|_| {
        LightWalletError::InvalidArgument {
            message: "Failure when parsing USK.".to_string(),
        }
    })?;
    RT.block_on(async move {
        let result = send_transaction(
            config.data_file,
            uri,
            config.network.into(),
            &usk,
            NonZeroU32::try_from(min_confirmations).map_err(|_| {
                Error::InvalidArgument("A positive integer is required.".to_string())
            })?,
            send_details,
        )
        .await?;
        Ok(SendTransactionResult {
            txid: result.txid.as_ref().to_vec(),
        })
    })
}

pub fn get_unshielded_utxos(
    config: DbInit,
    account_id: u32,
) -> Result<Vec<TransparentNote>, LightWalletError> {
    use crate::shield::get_unshielded_utxos;
    Ok(get_unshielded_utxos(
        config,
        account_id
            .try_into()
            .map_err(|_| Error::InvalidArgument("Invalid account id".to_string()))?,
    )?)
}

pub fn shield(
    config: DbInit,
    uri: String,
    usk: Vec<u8>,
    address: String,
) -> Result<SendTransactionResult, LightWalletError> {
    let uri: Uri = uri.parse()?;
    let usk = UnifiedSpendingKey::from_bytes(Era::Orchard, &usk).map_err(|_| {
        LightWalletError::InvalidArgument {
            message: "Failure when parsing USK.".to_string(),
        }
    })?;
    let network = Network::from(config.network);
    let address =
        TransparentAddress::decode(&network, &address[..]).map_err(|_| Error::InvalidAddress)?;
    RT.block_on(async move {
        Ok(SendTransactionResult {
            txid: shield_funds_at_address(config.data_file, uri, network, &usk, address)
                .await?
                .txid
                .as_ref()
                .to_vec(),
        })
    })
}

#[cfg(test)]
mod tests {
    use crate::test_constants::setup_test;

    use super::*;

    lazy_static! {
        static ref LIGHTSERVER_URI: Uri =
            crate::test_constants::TESTNET_LIGHTSERVER_ECC_URI.to_owned();
    }

    #[test]
    fn test_get_transactions_empty() {
        let setup = RT.block_on(async move { setup_test().await });
        let transactions = get_transactions(setup.db_init, 0, 0).unwrap();

        assert!(transactions.is_empty());
    }
}
