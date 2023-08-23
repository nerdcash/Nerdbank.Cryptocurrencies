use http::Uri;
use tokio::runtime::Runtime;
use zcash_primitives::consensus::BlockHeight;
use zingoconfig::ChainType;
use zingolib::lightclient::{LightClient, SyncResult};
use zingolib::load_clientconfig;

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

pub fn lightwallet_initialize(config: Config) -> Result<u64, LightWalletError> {
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

    // Initialize logging
    LightClient::init_logging().map_err(|e| LightWalletError::Other {
        message: e.to_string(),
    })?;

    // A new wallet has a birthday height matching the current blockchain height.
    let blockchain_height = zingolib::get_latest_block_height(server_uri.clone()).map_err(|e| {
        LightWalletError::Other {
            message: e.to_string(),
        }
    })?;

    let lightclient =
        match zingo_config.wallet_exists() {
            true => LightClient::read_wallet_from_disk(&zingo_config).map_err(|e| {
                LightWalletError::Other {
                    message: e.to_string(),
                }
            })?,
            // Use a birthday height that is somewhat less than the current blockchain length
            // to protect against re-orgs (?? why? a re-org cannot possibly reduce the chain length,
            // but sync malfunctions if the birthday height is within 100 blocks of the chain length).
            false => LightClient::new(&zingo_config, blockchain_height.saturating_sub(100))
                .map_err(|e| LightWalletError::Other {
                    message: e.to_string(),
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
    pub block_height: u32,
    pub is_incoming: bool,
}

pub fn lightwallet_get_transactions(
    handle: u64,
    starting_block: u32,
) -> Result<Vec<Transaction>, LightWalletError> {
    let lightclient = get_lightclient(handle).unwrap();
    Ok(RT.block_on(async move {
        lightclient
            .wallet
            .transactions()
            .read()
            .await
            .current
            .iter()
            .filter_map(|(txid, tx)| {
                println!("Transaction ID: {:?} {:?}", txid, tx);
                if tx.block_height >= BlockHeight::from_u32(starting_block) {
                    Some(Transaction {
                        txid: txid.to_string(),
                        block_height: tx.block_height.into(),
                        is_incoming: tx.is_incoming_transaction(),
                    })
                } else {
                    None
                }
            })
            .collect()
    }))
}
