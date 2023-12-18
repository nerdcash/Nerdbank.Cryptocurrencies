use http::{uri::InvalidUri, Uri};
use tokio::runtime::Runtime;
use zcash_primitives::consensus::Network;

use crate::{
    backend_client::sync, backing_store::init_db, error::Error, grpc::destroy_channel,
    lightclient::get_block_height,
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

pub struct Transaction {
    pub txid: String,
    pub datetime: u64,
    pub block_height: u32,
    pub is_incoming: bool,
    pub spent: u64,
    pub received: u64,
    pub price: Option<f64>,
    pub unconfirmed: bool,
    pub sends: Vec<TransactionSendDetail>,
    pub sapling_notes: Vec<SaplingNote>,
    pub orchard_notes: Vec<OrchardNote>,
}

#[derive(Debug)]
pub struct SaplingNote {
    pub value: u64,
    pub memo: Vec<u8>,
    pub is_change: bool,
    pub recipient: Vec<u8>,
}

#[derive(Debug)]
pub struct OrchardNote {
    pub value: u64,
    pub memo: Vec<u8>,
    pub is_change: bool,
    pub recipient: Vec<u8>,
}

#[derive(Debug)]
pub struct TransactionSendDetail {
    pub to_address: String,
    pub value: u64,
    pub memo: Vec<u8>,
}

#[derive(Debug, thiserror::Error)]
pub enum LightWalletError {
    #[error("Invalid URI")]
    InvalidUri,

    #[error("{message}")]
    Other { message: String },
}

impl From<InvalidUri> for LightWalletError {
    fn from(_: InvalidUri) -> Self {
        LightWalletError::InvalidUri
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

pub fn lightwallet_init(wallet_dir: String, network: ChainType) -> Result<(), LightWalletError> {
    RT.block_on(async move {
        init_db(wallet_dir, network.into()).await?;
        Ok(())
    })
}

pub fn lightwallet_get_block_height(uri: String) -> Result<u64, LightWalletError> {
    let uri: Uri = uri.parse()?;
    RT.block_on(async move { Ok(get_block_height(uri).await?) })
}

pub fn lightwallet_sync(uri: String, wallet_dir: String) -> Result<SyncResult, LightWalletError> {
    let uri: Uri = uri.parse()?;
    RT.block_on(async move { Ok(sync(uri, wallet_dir).await?) })
}

pub fn lightwallet_disconnect_server(uri: String) -> Result<bool, LightWalletError> {
    let uri: Uri = uri.parse()?;
    RT.block_on(async move { Ok(destroy_channel(uri)) })
}
