use http::Uri;
use zingoconfig::ChainType;
use zingolib::lightclient::LightClient;
use zingolib::load_clientconfig;

use std::collections::HashMap;
use std::ffi::{c_char, CStr};
use std::io::ErrorKind;
use std::path::PathBuf;
use std::sync::atomic::{AtomicIsize, Ordering};
use std::{cell::RefCell, sync::Arc, sync::Mutex};

// We'll use a MUTEX to store global lightclient instances, by handle,
// so we don't have to keep creating it. We need to store it here, in rust
// because we can't return such a complex structure back to our client.
lazy_static! {
    static ref LIGHTCLIENTS: Mutex<HashMap<isize, RefCell<Option<Arc<LightClient>>>>> =
        Mutex::new(HashMap::new());
    static ref LC_COUNTER: AtomicIsize = AtomicIsize::new(1);
}

fn add_lightclient(lightclient: Arc<LightClient>) -> isize {
    let mut clients = LIGHTCLIENTS.lock().unwrap();
    let handle = LC_COUNTER.fetch_add(1, Ordering::SeqCst);
    clients.insert(handle, RefCell::new(Some(lightclient)));
    handle
}

fn get_lightclient(handle: isize) -> Option<Arc<LightClient>> {
    let clients = LIGHTCLIENTS.lock().unwrap();
    if let Some(client) = clients.get(&handle) {
        let client_ref = client.borrow();
        if let Some(client) = &*client_ref {
            return Some(client.clone());
        }
    }
    None
}

fn remove_lightclient(handle: isize) -> bool {
    let mut clients = LIGHTCLIENTS.lock().unwrap();
    clients.remove(&handle).is_some()
}

pub fn lightwallet_get_block_height(server_uri: String) -> i64 {
    let lightwalletd_uri = Uri::try_from(server_uri);
    match lightwalletd_uri {
        Err(_) => -1,
        Ok(uri) => match zingolib::get_latest_block_height(uri) {
            Err(_) => -2,
            Ok(height) => height as i64,
        },
    }
}

/// Translates the C# ZcashNetwork enum into the rust equivalent.
fn from_zcash_network(network: i32) -> Result<ChainType, ErrorKind> {
    match network {
        0 => Ok(ChainType::Mainnet),
        1 => Ok(ChainType::Testnet),
        _ => Err(ErrorKind::InvalidInput),
    }
}

#[no_mangle]
pub extern "C" fn lightwallet_initialize(
    server_uri: *const c_char,
    network: i32,
    data_dir: *const c_char,
    wallet_name: *const c_char,
    log_name: *const c_char,
    monitor_mempool: u8,
) -> isize {
    let server_uri = unsafe { CStr::from_ptr(server_uri) };
    let server_uri = match Uri::try_from(server_uri.to_str().unwrap()) {
        Ok(uri) => uri,
        Err(_) => return -1,
    };

    let chain = match from_zcash_network(network) {
        Ok(n) => n,
        Err(_) => return -2,
    };

    let data_dir = unsafe { CStr::from_ptr(data_dir) };
    let data_dir = PathBuf::from(data_dir.to_str().unwrap());

    let wallet_name = unsafe { CStr::from_ptr(wallet_name) };

    let log_name = unsafe { CStr::from_ptr(log_name) };

    let monitor_mempool = monitor_mempool != 0;

    let mut config = match load_clientconfig(server_uri, Some(data_dir), chain, monitor_mempool) {
        Ok(c) => c,
        Err(_) => return -3,
    };
    config.wallet_name = wallet_name.to_str().unwrap().into();
    config.logfile_name = log_name.to_str().unwrap().into();

    // Initialize logging
    if LightClient::init_logging().is_err() {
        return -4;
    }

    let lightclient = match config.wallet_exists() {
        true => match LightClient::read_wallet_from_disk(&config) {
            Ok(l) => l,
            Err(_) => {
                return -5;
            }
        },
        false => match LightClient::new(&config, 0) {
            Ok(l) => l,
            Err(_) => {
                return -5;
            }
        },
    };

    let lc = Arc::new(lightclient);

    // We start mempool monitoring regardless of the input parameter because
    // this method itself no-op's if that value is false.
    LightClient::start_mempool_monitor(lc.clone());

    add_lightclient(lc)
}

#[no_mangle]
pub extern "C" fn lightwallet_deinitialize(handle: isize) -> i32 {
    match remove_lightclient(handle) {
        true => 0,
        false => -1,
    }
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
