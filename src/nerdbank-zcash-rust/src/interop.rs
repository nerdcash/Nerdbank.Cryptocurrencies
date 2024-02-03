use std::{num::NonZeroU32, time::SystemTime};

use http::{uri::InvalidUri, Uri};
use orchard::keys::Scope;
use rusqlite::{named_params, Connection};
use secrecy::SecretVec;
use tokio::runtime::Runtime;
use zcash_client_backend::{
    address::UnifiedAddress,
    data_api::WalletRead,
    encoding::AddressCodec,
    keys::{Era, UnifiedSpendingKey},
};
use zcash_client_sqlite::error::SqliteClientError;
use zcash_primitives::{consensus::Network, legacy::TransparentAddress, zip32::AccountId};

use crate::{
    analysis::{get_birthday_heights, get_user_balances, BirthdayHeights, UserBalances},
    backend_client::sync,
    backing_store::Db,
    error::Error,
    grpc::{destroy_channel, get_client},
    lightclient::get_block_height,
    send::send_transaction,
    shield::{get_unshielded_utxos, shield_funds_at_address},
    sql_statements::GET_TRANSACTIONS_SQL,
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

impl From<Network> for ChainType {
    fn from(network: Network) -> Self {
        match network {
            Network::MainNetwork => ChainType::Mainnet,
            Network::TestNetwork => ChainType::Testnet,
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
            Error::InternalError(msg) => LightWalletError::Other { message: msg },
            _ => LightWalletError::Other {
                message: e.to_string(),
            },
        }
    }
}

#[derive(Debug, Clone)]
pub struct SyncResult {
    pub latest_block: u64,
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

pub fn lightwallet_add_account(
    config: DbInit,
    uri: String,
    seed: Vec<u8>,
    birthday_height: Option<u32>,
) -> Result<u32, LightWalletError> {
    RT.block_on(async move {
        let mut db = Db::load(config.data_file, config.network.into())?;
        let mut client = get_client(uri.parse()?).await.map_err(Error::from)?;
        let birthday_height = match birthday_height {
            Some(v) => v,
            None => get_block_height(uri.parse()?).await?,
        };
        let secret = SecretVec::new(seed);
        let account = db
            .add_account(&secret, birthday_height as u64, &mut client)
            .await?;
        Ok(account.0.into())
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
                let memo = memo.unwrap_or(Vec::new());

                // Work out the receiving address when the sqlite db doesn't record it
                // but we have a diversifier that can regenerate it.
                if recipient.is_none() {
                    let diversifier: Option<Vec<u8>> = row.get("diversifier")?;
                    if let Some(diversifier) = diversifier {
                        recipient = match output_pool {
                            2 => ufvk
                                .map(|k| {
                                    k.sapling()
                                        .map(|s| {
                                            s.diversified_address(sapling::keys::Diversifier(
                                                diversifier.try_into().unwrap(),
                                            ))
                                            .map(|a| a.encode(&network))
                                        })
                                        .flatten()
                                })
                                .flatten(),
                            3 => ufvk
                                .map(|k| {
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

                if from_account == Some(account_id) && recipient.is_some() {
                    tx.outgoing.push(TransactionSendDetail {
                        recipient: recipient.unwrap(),
                        memo: Some(memo),
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

pub fn lightwallet_get_birthday_heights(
    config: DbInit,
    account_id: u32,
) -> Result<BirthdayHeights, LightWalletError> {
    Ok(get_birthday_heights(
        config,
        account_id
            .try_into()
            .map_err(|_| Error::InvalidArgument("bad account id".to_string()))?,
    )?)
}

pub fn lightwallet_get_user_balances(
    config: DbInit,
    account_id: u32,
    min_confirmations: u32,
) -> Result<UserBalances, LightWalletError> {
    Ok(get_user_balances(
        config,
        account_id
            .try_into()
            .map_err(|_| Error::InvalidArgument("Invalid account id".to_string()))?,
        NonZeroU32::try_from(min_confirmations)
            .map_err(|_| Error::InvalidArgument("A positive integer is required.".to_string()))?,
    )?)
}

pub fn lightwallet_disconnect_server(uri: String) -> Result<bool, LightWalletError> {
    let uri: Uri = uri.parse()?;
    RT.block_on(async move { Ok(destroy_channel(uri)) })
}

pub struct SendTransactionResult {
    pub txid: Vec<u8>,
}

pub fn lightwallet_send(
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

pub fn lightwallet_get_unshielded_utxos(
    config: DbInit,
    account_id: u32,
) -> Result<Vec<TransparentNote>, LightWalletError> {
    Ok(get_unshielded_utxos(
        config,
        account_id
            .try_into()
            .map_err(|_| Error::InvalidArgument("Invalid account id".to_string()))?,
    )?)
}

pub fn lightwallet_shield(
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
    fn test_lightwallet_get_transactions_empty() {
        let setup = RT.block_on(async move { setup_test().await });
        let transactions = lightwallet_get_transactions(setup.db_init, 0, 0).unwrap();

        assert!(transactions.len() == 0);
    }
}
