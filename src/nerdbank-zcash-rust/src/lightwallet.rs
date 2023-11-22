use http::Uri;
use tokio::runtime::Runtime;
use zcash_primitives::consensus::BlockHeight;
use zcash_primitives::memo::{Memo, MemoBytes};
use zingoconfig::ZingoConfig;
use zingolib::lightclient::{LightClient, PoolBalances, SyncResult};
use zingolib::load_clientconfig;
use zingolib::wallet::traits::{ReceivedNoteAndMetadata, ToBytes};
use zingolib::wallet::WalletBase;

use std::collections::HashMap;
use std::sync::atomic::{AtomicU64, Ordering};
use std::{cell::RefCell, sync::Arc, sync::Mutex};

const MARGINAL_FEE: u64 = 5_000; // From ZIP-317

// We'll use a MUTEX to store global lightclient instances, by handle,
// so we don't have to keep creating it. We need to store it here, in rust
// because we can't return such a complex structure back to our client.
lazy_static! {
    static ref LIGHTCLIENTS: Mutex<HashMap<u64, RefCell<Option<Arc<LightClient>>>>> =
        Mutex::new(HashMap::new());
    static ref LC_COUNTER: AtomicU64 = AtomicU64::new(1);
}

lazy_static! {
    static ref RT: Runtime = tokio::runtime::Runtime::new().unwrap();
}

pub enum ChainType {
    Mainnet,
    Testnet,
}

impl From<ChainType> for zingoconfig::ChainType {
    fn from(chain_type: ChainType) -> Self {
        match chain_type {
            ChainType::Mainnet => zingoconfig::ChainType::Mainnet,
            ChainType::Testnet => zingoconfig::ChainType::Testnet,
        }
    }
}

fn add_lightclient(lightclient: Arc<LightClient>) -> u64 {
    let mut clients = LIGHTCLIENTS.lock().unwrap();
    let handle = LC_COUNTER.fetch_add(1, Ordering::SeqCst);
    clients.insert(handle, RefCell::new(Some(lightclient)));
    handle
}

fn get_lightclient(handle: u64) -> Result<Arc<LightClient>, LightWalletError> {
    let clients = LIGHTCLIENTS.lock().unwrap();
    if let Some(client) = clients.get(&handle) {
        let client_ref = client.borrow();
        if let Some(client) = &*client_ref {
            return Ok(client.clone());
        }
    }
    Err(LightWalletError::InvalidHandle)
}

fn remove_lightclient(handle: u64) -> bool {
    let mut clients = LIGHTCLIENTS.lock().unwrap();
    clients.remove(&handle).is_some()
}

#[derive(Debug, thiserror::Error)]
pub enum LightWalletError {
    #[error("Invalid URI")]
    InvalidUri,

    #[error("Invalid handle")]
    InvalidHandle,

    #[error("{message}")]
    Other { message: String },
}

pub fn lightwallet_get_block_height(server_uri: String) -> Result<u64, LightWalletError> {
    let server_uri = Uri::try_from(server_uri).map_err(|_| LightWalletError::InvalidUri)?;
    Ok(
        zingolib::get_latest_block_height(server_uri).map_err(|err| LightWalletError::Other {
            message: err.to_string(),
        })?,
    )
}

pub struct Config {
    pub server_uri: String,
    pub chain_type: ChainType,
    pub data_dir: String,
    pub wallet_name: String,
    pub log_name: String,
    pub monitor_mempool: bool,
    pub minimum_confirmations: u32,
}

pub struct WalletInfo {
    pub ufvk: Option<String>,
    pub unified_spending_key: Option<Vec<u8>>,
    pub birthday_height: u64,
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
/// If fairy dust is sent to the wallet, the simpler view's Incoming balance would include it,
/// only for it to evaporate when confirmed.
/// But incoming can always evaporate (e.g. a transaction expires before confirmation),
/// and the alternatives being to either hide that a transmission was made at all, or to include
/// the fairy dust in other balances could be more misleading.
///
/// An app *could* choose to prominently warn the user if a significant proportion of the incoming balance is fairy dust,
/// although this event seems very unlikely since it will cost the sender *more* than the amount the recipient is expecting
/// to 'fool' them into thinking they are receiving value.
/// The more likely scenario is that the sender is trying to send a small amount of value as a new user and doesn't realize
/// the value is too small to be useful.
/// A good Zcash wallet should prevent sending fairy dust in the first place.
pub struct UserBalances {
    /// Available for immediate spending.
    /// Expected fees are *not* deducted from this value, but the app may do so by subtracting `minimum_fees`.
    /// `fairy_dust` is excluded from this value.
    ///
    /// For enhanced privacy, the minimum number of required confirmations to spend a note is usually greater than one.
    pub spendable: u64,

    /// The sum of the change notes that have insufficient confirmations to be spent.
    pub immature_change: u64,

    /// The minimum fees that can be expected to spend all `spendable + immature_change` funds in the wallet.
    /// This fee assumes all funds will be sent to a single note.
    ///
    /// Balances described by other fields in this struct are not included because they are not confirmed,
    /// they may amount to fairy dust, or because as `immature_income` funds they may require shielding which has a cost
    /// and can change the amount of fees required to spend them (e.g. 3 UTXOs shielded together become only 1 note).
    pub minimum_fees: u64,

    /// The sum of non-change notes with a non-zero confirmation count that is less than the minimum required for spending,
    /// and all UTXOs (considering that UTXOs must be shielded before spending).
    /// `fairy_dust` is excluded from this value.
    ///
    /// As funds mature, this may not be the exact amount added to `spendable`, since the process of maturing
    /// may require shielding, which has a cost.
    pub immature_income: u64,

    /// The sum of all *confirmed* UTXOs and notes that are worth less than the fee to spend them,
    /// making them essentially inaccessible.
    pub fairy_dust: u64,

    /// The sum of all *unconfirmed* UTXOs and notes that are not change.
    /// This value includes any applicable `incoming_fairy_dust`.
    pub incoming: u64,

    /// The sum of all *unconfirmed* UTXOs and notes that are not change and are each counted as fairy dust.
    pub incoming_fairy_dust: u64,
}

fn prepare_config(config: Config) -> Result<ZingoConfig, LightWalletError> {
    let server_uri = Uri::try_from(config.server_uri).map_err(|e| LightWalletError::Other {
        message: e.to_string(),
    })?;

    let mut zingo_config = load_clientconfig(
        server_uri.clone(),
        Some(config.data_dir.into()),
        config.chain_type.into(),
        config.monitor_mempool,
    )
    .map_err(|e| LightWalletError::Other {
        message: e.to_string(),
    })?;
    zingo_config.wallet_name = config.wallet_name.into();
    zingo_config.logfile_name = config.log_name.into();
    zingo_config.reorg_buffer_offset = config.minimum_confirmations - 1; // zingolib requires this number, plus 1

    Ok(zingo_config)
}
pub fn lightwallet_initialize(
    config: Config,
    wallet_info: WalletInfo,
) -> Result<u64, LightWalletError> {
    let zingo_config = prepare_config(config)?;

    let lightclient = match zingo_config.wallet_path_exists() {
        true => LightClient::read_wallet_from_disk(&zingo_config).map_err(|e| {
            LightWalletError::Other {
                message: e.to_string(),
            }
        })?,
        false => RT.block_on(async move {
            LightClient::create_from_wallet_base_async(
                if wallet_info.unified_spending_key.is_some() {
                    WalletBase::Usk(wallet_info.unified_spending_key.unwrap())
                } else if wallet_info.ufvk.is_some() {
                    WalletBase::Ufvk(wallet_info.ufvk.unwrap())
                } else {
                    return Err(LightWalletError::Other {
                        message: "No wallet info provided".to_string(),
                    });
                },
                &zingo_config,
                wallet_info.birthday_height,
                false,
            )
            .await
            .map_err(|e| LightWalletError::Other {
                message: e.to_string(),
            })
        })?,
    };

    let lc = Arc::new(lightclient);

    // We start mempool monitoring regardless of the input parameter because
    // this method itself no-op's if that value is false.
    LightClient::start_mempool_monitor(lc.clone());

    Ok(add_lightclient(lc))
}

pub fn lightwallet_initialize_from_disk(config: Config) -> Result<u64, LightWalletError> {
    let zingo_config = prepare_config(config)?;

    let lightclient =
        LightClient::read_wallet_from_disk(&zingo_config).map_err(|e| LightWalletError::Other {
            message: e.to_string(),
        })?;

    let lc = Arc::new(lightclient);

    // We start mempool monitoring regardless of the input parameter because
    // this method itself no-op's if that value is false.
    LightClient::start_mempool_monitor(lc.clone());

    Ok(add_lightclient(lc))
}

pub fn lightwallet_deinitialize(handle: u64) -> bool {
    remove_lightclient(handle)
}

pub fn lightwallet_get_birthday_height(handle: u64) -> Result<u64, LightWalletError> {
    let lightclient = get_lightclient(handle)?;
    RT.block_on(async move { Ok(lightclient.wallet.get_birthday().await) })
}

pub fn last_synced_height(handle: u64) -> Result<u64, LightWalletError> {
    let lightclient = get_lightclient(handle)?;
    RT.block_on(async move { Ok(lightclient.wallet.last_synced_height().await) })
}

pub fn lightwallet_sync(handle: u64) -> Result<SyncResult, LightWalletError> {
    let lightclient = get_lightclient(handle)?;

    RT.block_on(async move {
        let sync_result =
            lightclient
                .do_sync(false)
                .await
                .map_err(|e| LightWalletError::Other {
                    message: e.to_string(),
                })?;
        Ok(sync_result)
    })
}

pub fn lightwallet_sync_interrupt(handle: u64) -> Result<(), LightWalletError> {
    let lightclient = get_lightclient(handle)?;
    RT.block_on(async move {
        lightclient.interrupt_sync_after_batch(true).await;
    });
    Ok(())
}

#[derive(Clone, Debug, Default)]
pub struct SyncStatus {
    pub in_progress: bool,
    pub last_error: Option<String>,

    pub sync_id: u64,
    pub start_block: u64,
    pub end_block: u64,

    pub blocks_done: u64,
    pub trial_dec_done: u64,
    pub txn_scan_done: u64,

    pub blocks_total: u64,

    pub batch_num: u64,
    pub batch_total: u64,
}

pub fn lightwallet_sync_status(handle: u64) -> Result<SyncStatus, LightWalletError> {
    let lightclient = get_lightclient(handle)?;
    Ok(RT.block_on(async move {
        let status = lightclient.do_sync_status().await;
        SyncStatus {
            batch_num: status.batch_num as u64,
            batch_total: status.batch_total as u64,
            blocks_done: status.blocks_done,
            trial_dec_done: status.trial_dec_done,
            txn_scan_done: status.txn_scan_done,
            blocks_total: status.blocks_total,
            in_progress: status.in_progress,
            last_error: status.last_error,
            sync_id: status.sync_id,
            start_block: status.start_block,
            end_block: status.end_block,
        }
    }))
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
pub struct TransactionSendDetail {
    pub to_address: String,
    pub value: u64,
    pub recipient_ua: Option<String>,
    pub memo: Vec<u8>,
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

pub fn lightwallet_get_transactions(
    handle: u64,
    starting_block: u32,
) -> Result<Vec<Transaction>, LightWalletError> {
    let lightclient = get_lightclient(handle)?;
    Ok(RT.block_on(async move {
        lightclient
            .wallet
            .transactions()
            .read()
            .await
            .current
            .iter()
            .filter_map(|(txid, tx)| {
                if tx.block_height >= BlockHeight::from_u32(starting_block) {
                    Some(Transaction {
                        txid: txid.to_string(),
                        datetime: tx.datetime,
                        block_height: tx.block_height.into(),
                        is_incoming: tx.is_incoming_transaction(),
                        spent: tx.total_value_spent(),
                        received: tx.total_value_received(),
                        price: tx.price,
                        unconfirmed: tx.unconfirmed,
                        sends: tx
                            .outgoing_tx_data
                            .iter()
                            .map(|o| TransactionSendDetail {
                                to_address: o.to_address.to_string(),
                                value: o.value,
                                recipient_ua: o.recipient_ua.clone(),
                                memo: o.memo.to_bytes().into(),
                            })
                            .collect(),
                        sapling_notes: tx
                            .sapling_notes
                            .iter()
                            .map(|n| SaplingNote {
                                value: n.note.value().inner(),
                                memo: n.memo.as_ref().unwrap_or(&Memo::Empty).to_bytes().into(),
                                is_change: n.is_change,
                                recipient: n.note.recipient().to_bytes().to_vec(),
                            })
                            .collect(),
                        orchard_notes: tx
                            .orchard_notes
                            .iter()
                            .map(|n| OrchardNote {
                                value: n.note.value().inner(),
                                memo: n.memo.as_ref().unwrap_or(&Memo::Empty).to_bytes().into(),
                                is_change: n.is_change,
                                recipient: n.note.recipient().to_raw_address_bytes().to_vec(),
                            })
                            .collect(),
                    })
                } else {
                    None
                }
            })
            .collect()
    }))
}

pub fn lightwallet_send_to_address(
    handle: u64,
    address_amount_memo_tuples: Vec<TransactionSendDetail>,
) -> Result<String, LightWalletError> {
    let lightclient = get_lightclient(handle)?;
    let mut error = None;
    let address_amount_memo_tuples = address_amount_memo_tuples
        .iter()
        .map(|f| {
            let memo = match MemoBytes::from_bytes(f.memo.as_slice()) {
                Ok(m) => Some(m),
                Err(e) => {
                    error = Some(e);
                    None
                }
            };
            (&f.to_address[..], f.value, memo)
        })
        .collect();
    if error.is_some() {
        return Err(LightWalletError::Other {
            message: error.unwrap().to_string(),
        });
    }

    RT.block_on(async move {
        lightclient
            .do_send(address_amount_memo_tuples)
            .await
            .map_err(|o| LightWalletError::Other {
                message: o.to_string(),
            })
    })
}

pub struct SendUpdate {
    pub id: u32,
    pub is_send_in_progress: bool,
    pub progress: u32,
    pub total: u32,
    pub last_error: Option<String>,
    pub last_transaction_id: Option<String>,
}

pub fn lightwallet_send_check_status(handle: u64) -> Result<SendUpdate, LightWalletError> {
    let lightclient = get_lightclient(handle)?;
    RT.block_on(async move {
        let update = lightclient
            .do_send_progress()
            .await
            .map_err(|o| LightWalletError::Other {
                message: o.to_string(),
            })?;
        Ok(SendUpdate {
            progress: update.progress.progress,
            total: update.progress.total,
            id: update.progress.id,
            is_send_in_progress: update.progress.is_send_in_progress,
            last_error: update.progress.last_error,
            last_transaction_id: update.progress.last_transaction_id,
        })
    })
}

pub fn lightwallet_get_balances(handle: u64) -> Result<PoolBalances, LightWalletError> {
    let lightclient = get_lightclient(handle)?;
    Ok(RT.block_on(async move { lightclient.do_balance().await }))
}

pub fn lightwallet_get_user_balances(handle: u64) -> Result<UserBalances, LightWalletError> {
    let lightclient = get_lightclient(handle)?;

    let mut balances = UserBalances {
        spendable: 0,
        immature_change: 0,
        minimum_fees: 0,
        immature_income: 0,
        fairy_dust: 0,
        incoming: 0,
        incoming_fairy_dust: 0,
    };

    RT.block_on(async move {
        // anchor height is the highest block height that contains income that are considered spendable.
        let anchor_height = lightclient.wallet.get_anchor_height().await;

        lightclient
            .wallet
            .transactions()
            .read()
            .await
            .current
            .iter()
            .for_each(|(_, tx)| {
                let mature =
                    !tx.unconfirmed && tx.block_height <= BlockHeight::from_u32(anchor_height);
                let incoming = tx.is_incoming_transaction();

                let mut change = 0;
                let mut useful_value = 0;
                let mut fairy_dust_value = 0;
                let mut utxo_value = 0;
                let mut inbound_note_count_nodust = 0;
                let mut change_note_count = 0;

                tx.orchard_notes
                    .iter()
                    .filter(|n| n.spent().is_none() && n.unconfirmed_spent.is_none())
                    .for_each(|n| {
                        let value = n.note.value().inner();
                        if !incoming && n.is_change {
                            change += value;
                            change_note_count += 1;
                        } else if incoming {
                            if value > MARGINAL_FEE {
                                useful_value += value;
                                inbound_note_count_nodust += 1;
                            } else {
                                fairy_dust_value += value;
                            }
                        }
                    });

                tx.sapling_notes
                    .iter()
                    .filter(|n| n.spent().is_none() && n.unconfirmed_spent.is_none())
                    .for_each(|n| {
                        let value = n.note.value().inner();
                        if !incoming && n.is_change {
                            change += value;
                            change_note_count += 1;
                        } else if incoming {
                            if value > MARGINAL_FEE {
                                useful_value += value;
                                inbound_note_count_nodust += 1;
                            } else {
                                fairy_dust_value += value;
                            }
                        }
                    });

                tx.received_utxos
                    .iter()
                    .filter(|n| n.spent.is_none() && n.unconfirmed_spent.is_none())
                    .for_each(|n| {
                        // UTXOs are never 'change', as change would have been shielded.
                        if incoming {
                            if n.value > MARGINAL_FEE {
                                utxo_value += n.value;
                                inbound_note_count_nodust += 1;
                            } else {
                                fairy_dust_value += n.value;
                            }
                        }
                    });

                // The fee field only tracks mature income and change.
                balances.minimum_fees += change_note_count * MARGINAL_FEE;
                if mature {
                    balances.minimum_fees += inbound_note_count_nodust * MARGINAL_FEE;
                }

                if mature {
                    // Spendable
                    balances.spendable += useful_value + change;
                    balances.fairy_dust += fairy_dust_value;
                    balances.immature_income += utxo_value; // UTXOs are always immature, since they should be shielded before spending.
                } else if !tx.unconfirmed {
                    // Confirmed, but not yet spendable
                    balances.immature_income += useful_value + utxo_value;
                    balances.immature_change += change;
                    balances.fairy_dust += fairy_dust_value;
                } else {
                    // Unconfirmed
                    balances.immature_change += change;
                    balances.incoming += useful_value + utxo_value;
                    balances.incoming_fairy_dust += fairy_dust_value;
                }
            });

        // Add the minimum fee for the receiving note,
        // but only if there exists notes to spend in the buckets that are covered by the minimum_fee.
        if balances.minimum_fees > 0 {
            balances.minimum_fees += MARGINAL_FEE; // The receiving note.
        }

        Ok(balances)
    })
}
