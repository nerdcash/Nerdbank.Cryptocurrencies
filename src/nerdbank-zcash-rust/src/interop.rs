use std::{
    collections::HashMap,
    sync::{
        Mutex,
        atomic::{AtomicU32, Ordering},
    },
    time::SystemTime,
};

use http::{Uri, uri::InvalidUri};
use rusqlite::Connection;
use secrecy::SecretVec;
use tokio::runtime::Runtime;
use tokio_util::sync::CancellationToken;
use uuid::Uuid;
use zcash_client_backend::{
    data_api::{Account, AccountPurpose, WalletRead, wallet::ConfirmationsPolicy},
    encoding::AddressCodec,
    keys::{Era, UnifiedSpendingKey},
};
use zcash_client_sqlite::{AccountUuid, error::SqliteClientError};
use zcash_keys::keys::UnifiedFullViewingKey;
use zcash_protocol::{consensus::Network, memo::MemoBytes, value::Zatoshis};
use zcash_transparent::address::TransparentAddress;
use zip32::DiversifierIndex;

use crate::{
    analysis::{BirthdayHeights, UserBalances},
    backing_store::Db,
    error::Error,
    grpc::{destroy_channel, get_client},
    send::{create_send_proposal, send_transaction},
    shield::shield_funds,
};

lazy_static! {
    static ref CRYPTO_INIT: () = {
        // Initialize the rustls crypto provider before any operations
        let _ = rustls::crypto::aws_lc_rs::default_provider().install_default();
    };
    static ref RT: Runtime = {
        // Ensure crypto is initialized
        let _ = *CRYPTO_INIT;
        tokio::runtime::Runtime::new().unwrap()
    };
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

#[derive(Debug, Copy, Clone)]
pub enum Pool {
    Transparent,
    Sapling,
    Orchard,
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
    pub id: Vec<u8>,
    pub uvk: Option<String>,
    pub birthday_heights: BirthdayHeights,
}

#[derive(Debug, Clone)]
pub struct Transaction {
    pub account_id: Vec<u8>,
    pub txid: Vec<u8>,
    pub block_time: Option<SystemTime>,
    pub mined_height: Option<u32>,
    pub expired_unmined: bool,
    pub account_balance_delta: i64,
    pub fee: Option<u64>,
    /// Notes that are sent by this transaction (and do not appear in `change`).
    pub outgoing: Vec<TransactionNote>,
    /// Notes that are received by this transaction (and do not appear in `change`).
    pub incoming: Vec<TransactionNote>,
    /// Notes that are sent and received by the same account and bear other signs of being implicit change.
    pub change: Vec<TransactionNote>,
}

#[derive(Debug, Clone)]
pub struct TransparentNote {
    pub value: u64,
    pub recipient: String,
}

#[derive(Debug, Clone)]
pub struct TransactionNote {
    pub recipient: String,
    pub pool: Pool,
    pub value: u64,
    pub memo: Option<Vec<u8>>,
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

    #[error("Sync first.")]
    SyncFirst,

    #[error("Insufficient funds: {required} required but only {available} is available.")]
    InsufficientFunds { required: u64, available: u64 },

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
            Error::Canceled => LightWalletError::Canceled,
            Error::InvalidArgument(msg) => LightWalletError::InvalidArgument { message: msg },
            Error::Internal(msg) => LightWalletError::Other { message: msg },
            Error::InsufficientFunds {
                required,
                available,
            } => LightWalletError::InsufficientFunds {
                required: required.into(),
                available: available.into(),
            },
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
    name: String,
    seed: Vec<u8>,
    zip32_account_index: u32,
    birthday_height: Option<u32>,
    cancellation: Option<Box<dyn CancellationSource>>,
) -> Result<Vec<u8>, LightWalletError> {
    use crate::lightclient::get_block_height;
    let cancellation_token = get_cancellation_token(cancellation)?;
    let account_index = zip32::AccountId::try_from(zip32_account_index).map_err(|_| {
        LightWalletError::InvalidArgument {
            message: "Invalid account index".to_string(),
        }
    })?;

    RT.block_on(async move {
        let mut db = Db::load(config.data_file, config.network.into())?;
        let mut client = get_client(uri.parse()?).await.map_err(Error::from)?;
        let birthday_height = match birthday_height {
            Some(v) => v,
            None => get_block_height(uri.parse()?, cancellation_token.0.clone()).await?,
        };
        let secret = SecretVec::new(seed);
        let account = db
            .add_account(
                &name,
                &secret,
                account_index,
                birthday_height as u64,
                &mut client,
            )
            .await?;
        Ok(uuid_to_bytes(&account.0.id()))
    })
}

pub fn import_account_ufvk(
    config: DbInit,
    uri: String,
    name: String,
    ufvk: String,
    spending_key_available: bool,
    birthday_height: Option<u32>,
    cancellation: Option<Box<dyn CancellationSource>>,
) -> Result<Vec<u8>, LightWalletError> {
    use crate::lightclient::get_block_height;
    let cancellation_token = get_cancellation_token(cancellation)?;
    let network: Network = config.network.into();
    RT.block_on(async move {
        let mut db = Db::load(config.data_file, config.network.into())?;
        let mut client = get_client(uri.parse()?).await.map_err(Error::from)?;
        let birthday_height = match birthday_height {
            Some(v) => v,
            None => get_block_height(uri.parse()?, cancellation_token.0.clone()).await?,
        };
        let ufvk = UnifiedFullViewingKey::decode(&network, ufvk.as_str()).map_err(|e| {
            LightWalletError::InvalidArgument {
                message: format!("Invalid UFVK: {e}"),
            }
        })?;
        let account = db
            .import_account_ufvk(
                &name,
                &ufvk,
                if spending_key_available {
                    AccountPurpose::Spending { derivation: None }
                } else {
                    AccountPurpose::ViewOnly
                },
                birthday_height as u64,
                None,
                &mut client,
            )
            .await?;
        Ok(uuid_to_bytes(&account.id()))
    })
}

pub fn get_accounts(config: DbInit) -> Result<Vec<AccountInfo>, LightWalletError> {
    use crate::analysis::get_birthday_heights;

    let db = Db::load(config.data_file.clone(), config.network.into())?;
    let network: Network = config.network.into();
    let mut result = Vec::new();
    for account_info in db.data.get_unified_full_viewing_keys()?.iter() {
        result.push(AccountInfo {
            id: uuid_to_bytes(&account_info.0),
            uvk: Some(account_info.1.encode(&network)),
            birthday_heights: get_birthday_heights(config.clone(), account_info.0)?,
        });
    }

    Ok(result)
}

fn uuid_to_bytes(account_uuid: &AccountUuid) -> Vec<u8> {
    account_uuid.expose_uuid().as_bytes().to_vec()
}

fn bytes_to_uuid(account_id: Vec<u8>) -> Result<AccountUuid, LightWalletError> {
    Ok(AccountUuid::from_uuid(Uuid::from_bytes(
        account_id
            .try_into()
            .map_err(|_| LightWalletError::InvalidArgument {
                message: "Invalid account ID".to_string(),
            })?,
    )))
}

pub fn add_diversifier(
    config: DbInit,
    account_id: Vec<u8>,
    diversifier_index: Vec<u8>,
) -> Result<String, LightWalletError> {
    let account_id = bytes_to_uuid(account_id)?;
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
            .add_diversifier(account_id, diversifier_index)?
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
            ConfirmationsPolicy::default(),
            continually,
            cancellation_token.0.clone(),
        )
        .await?)
    })
}

pub fn get_transactions(
    config: DbInit,
    account_id: Vec<u8>,
    starting_block: u32,
) -> Result<Vec<Transaction>, LightWalletError> {
    let network: Network = config.network.into();
    let mut db = Db::load(config.data_file.clone(), network)?;
    let mut conn = Connection::open(config.data_file)?;
    let account_id = bytes_to_uuid(account_id)?;
    Ok(crate::sync::get_transactions(
        &mut db,
        &mut conn,
        &network,
        Some(account_id),
        Some(starting_block),
        None,
    )?)
}

pub fn get_birthday_heights(
    config: DbInit,
    account_id: Vec<u8>,
) -> Result<BirthdayHeights, LightWalletError> {
    use crate::analysis::get_birthday_heights;

    let account_id = bytes_to_uuid(account_id)?;
    Ok(get_birthday_heights(config, &account_id)?)
}

pub fn get_user_balances(
    config: DbInit,
    account_id: Vec<u8>,
) -> Result<UserBalances, LightWalletError> {
    use crate::analysis::get_user_balances;
    let account_id = bytes_to_uuid(account_id)?;
    Ok(get_user_balances(
        &config,
        &account_id,
        ConfirmationsPolicy::default(),
    )?)
}

pub fn disconnect_server(uri: String) -> Result<bool, LightWalletError> {
    let uri: Uri = uri.parse()?;
    RT.block_on(async move { Ok(destroy_channel(uri)) })
}

pub struct SendTransactionResult {
    pub txid: Vec<u8>,
}

pub struct SendDetails {
    pub fee: u64,
}

pub fn simulate_send(
    config: DbInit,
    ufvk: String,
    send_details: Vec<TransactionSendDetail>,
) -> Result<SendDetails, LightWalletError> {
    let network = config.network.into();
    let mut db = Db::init(config.data_file, network)?;
    let ufvk = UnifiedFullViewingKey::decode(&network, &ufvk)
        .map_err(|s| LightWalletError::InvalidArgument { message: s })?;
    let proposal = create_send_proposal(&mut db, network, &ufvk, send_details)?;

    Ok(SendDetails {
        fee: proposal
            .steps()
            .iter()
            .map(|s| Into::<u64>::into(s.balance().fee_required()))
            .sum(),
    })
}

pub fn send(
    config: DbInit,
    uri: String,
    usk: Vec<u8>,
    send_details: Vec<TransactionSendDetail>,
) -> Result<Vec<SendTransactionResult>, LightWalletError> {
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
            send_details,
        )
        .await?;
        Ok(result
            .map(|r| SendTransactionResult {
                txid: r.txid.as_ref().to_vec(),
            })
            .into_iter()
            .collect::<Vec<_>>())
    })
}

pub fn get_unshielded_utxos(
    config: DbInit,
    account_id: Vec<u8>,
) -> Result<Vec<TransparentNote>, LightWalletError> {
    use crate::shield::get_unshielded_utxos;
    let account_id = bytes_to_uuid(account_id)?;
    Ok(get_unshielded_utxos(config, account_id)?)
}

pub fn shield(
    config: DbInit,
    uri: String,
    account_id: Vec<u8>,
    usk: Vec<u8>,
    address: Option<String>,
    shielding_threshold: u64,
    memo: Option<Vec<u8>>,
) -> Result<Vec<SendTransactionResult>, LightWalletError> {
    let uri: Uri = uri.parse()?;
    let account_id = bytes_to_uuid(account_id)?;
    let usk = UnifiedSpendingKey::from_bytes(Era::Orchard, &usk).map_err(|_| {
        LightWalletError::InvalidArgument {
            message: "Failure when parsing USK.".to_string(),
        }
    })?;
    let network = Network::from(config.network);
    let address = address
        .map(|a| TransparentAddress::decode(&network, &a[..]).map_err(|_| Error::InvalidAddress))
        .transpose()?;
    let shielding_threshold = Zatoshis::from_u64(shielding_threshold).map_err(|_| {
        Error::InvalidArgument("Shielding threshold exceeds maximum Zatoshis count.".to_string())
    })?;
    let memo = memo
        .map(|m| {
            MemoBytes::from_bytes(&m)
                .map_err(|e| Error::InvalidArgument(format!("Invalid memo: {e}")))
        })
        .transpose()?;
    RT.block_on(async move {
        Ok(shield_funds(
            config.data_file,
            uri,
            network,
            account_id,
            &usk,
            shielding_threshold,
            address,
            memo,
        )
        .await?
        .map(|r| SendTransactionResult {
            txid: r.txid.as_ref().to_vec(),
        })
        .into_iter()
        .collect::<Vec<_>>())
    })
}

#[cfg(test)]
mod tests {
    use crate::test_constants::setup_test;

    use super::*;

    lazy_static! {
        static ref LIGHTSERVER_URI: Uri = crate::test_constants::TESTNET_LIGHTSERVER_URI.to_owned();
    }

    #[test]
    fn test_get_transactions_empty() {
        RT.block_on(async move {
            let mut setup = setup_test().await;
            let account_id = setup.create_account().await.unwrap();
            let account_id_bytes = uuid_to_bytes(&account_id.2);
            let transactions = get_transactions(setup.db_init, account_id_bytes, 0).unwrap();
            assert!(transactions.is_empty());
        });
    }
}
