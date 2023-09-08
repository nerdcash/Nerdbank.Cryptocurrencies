use http::Uri;
use tokio::runtime::Runtime;
use zcash_primitives::consensus::BlockHeight;
use zcash_primitives::memo::MemoBytes;
use zingoconfig::ChainType;
use zingolib::lightclient::{LightClient, SyncResult};
use zingolib::load_clientconfig;
use zingolib::wallet::traits::ToBytes;
use zingolib::wallet::WalletBase;

use std::collections::HashMap;
use std::sync::atomic::{AtomicU64, Ordering};
use std::{cell::RefCell, sync::Arc, sync::Mutex};

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
}

pub struct WalletInfo {
    pub ufvk: Option<String>,
    pub unified_spending_key: Option<Vec<u8>>,
    pub birthday_height: u64,
}

pub fn lightwallet_initialize(
    config: Config,
    wallet_info: WalletInfo,
) -> Result<u64, LightWalletError> {
    let server_uri = Uri::try_from(config.server_uri).map_err(|e| LightWalletError::Other {
        message: e.to_string(),
    })?;

    let mut zingo_config = load_clientconfig(
        server_uri.clone(),
        Some(config.data_dir.into()),
        config.chain_type,
        config.monitor_mempool,
    )
    .map_err(|e| LightWalletError::Other {
        message: e.to_string(),
    })?;
    zingo_config.wallet_name = config.wallet_name.into();
    zingo_config.logfile_name = config.log_name.into();
    zingo_config.reorg_buffer_offset = 2; // 2+1=3 confirmations before notes can be spent.

    // Initialize logging
    LightClient::init_logging().map_err(|e| LightWalletError::Other {
        message: e.to_string(),
    })?;

    let lightclient = match zingo_config.wallet_exists() {
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
}

#[derive(Debug)]
pub struct TransactionSendDetail {
    pub to_address: String,
    pub value: u64,
    pub recipient_ua: Option<String>,
    pub memo: Vec<u8>,
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
