use http::Uri;
use zingoconfig::ChainType;
use zingolib::lightclient::LightClient;
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

fn add_lightclient(lightclient: Arc<LightClient>) -> u64 {
    let mut clients = LIGHTCLIENTS.lock().unwrap();
    let handle = LC_COUNTER.fetch_add(1, Ordering::SeqCst);
    clients.insert(handle, RefCell::new(Some(lightclient)));
    handle
}

fn get_lightclient(handle: u64) -> Option<Arc<LightClient>> {
    let clients = LIGHTCLIENTS.lock().unwrap();
    if let Some(client) = clients.get(&handle) {
        let client_ref = client.borrow();
        if let Some(client) = &*client_ref {
            return Some(client.clone());
        }
    }
    None
}

fn remove_lightclient(handle: u64) -> bool {
    let mut clients = LIGHTCLIENTS.lock().unwrap();
    clients.remove(&handle).is_some()
}

#[derive(Debug, thiserror::Error)]
pub enum LightWalletError {
    #[error("Invalid URI")]
    InvalidUri,

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

pub enum Network {
    MainNet,
    TestNet,
}

impl From<Network> for ChainType {
    fn from(network: Network) -> Self {
        match network {
            Network::MainNet => ChainType::Mainnet,
            Network::TestNet => ChainType::Testnet,
        }
    }
}

pub struct Config {
    pub server_uri: String,
    pub network: Network,
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
        server_uri,
        Some(config.data_dir.into()),
        config.network.into(),
        config.monitor_mempool,
    ).map_err(|e| LightWalletError::Other { message: e.to_string() })?;
    zingo_config.wallet_name = config.wallet_name.into();
    zingo_config.logfile_name = config.log_name.into();

    // Initialize logging
    LightClient::init_logging().map_err(|e| LightWalletError::Other { message: e.to_string() })?;

    let lightclient = match zingo_config.wallet_exists() {
        true => LightClient::read_wallet_from_disk(&zingo_config).map_err(|e| LightWalletError::Other { message: e.to_string() })?,
        false => LightClient::new(&zingo_config, 0).map_err(|e| LightWalletError::Other { message: e.to_string() })?,
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

// pub fn exec(cmd: String, args_list: String) -> String {
//     let lightclient: Arc<LightClient>;
//     {
//         let lc = LIGHTCLIENT.lock().unwrap();

//         if lc.borrow().is_none() {
//             return format!("Error: Light Client is not initialized");
//         }

//         lightclient = lc.borrow().as_ref().unwrap().clone();
//     };

//     if cmd == "sync" || cmd == "rescan" || cmd == "import" {
//         thread::spawn(move || {
//             let args = vec![&args_list[..]];
//             commands::do_user_command(&cmd, &args, lightclient.as_ref());
//         });

//         "OK".to_string()
//     } else {
//         let args = if args_list.is_empty() {
//             vec![]
//         } else {
//             vec![&args_list[..]]
//         };
//         commands::do_user_command(&cmd, &args, lightclient.as_ref()).clone()
//     }
// }
