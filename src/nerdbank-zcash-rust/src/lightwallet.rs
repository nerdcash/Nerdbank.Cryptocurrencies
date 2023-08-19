use http::Uri;
use zingolib::lightclient::LightClient;

use std::ffi::{c_char, CStr};
use std::{cell::RefCell, sync::Arc, sync::Mutex};

// We'll use a MUTEX to store a global lightclient instance,
// so we don't have to keep creating it. We need to store it here, in rust
// because we can't return such a complex structure back to our client.
lazy_static! {
    static ref LIGHTCLIENT: Mutex<RefCell<Option<Arc<LightClient>>>> =
        Mutex::new(RefCell::new(None));
}

#[no_mangle]
pub extern "C" fn lightwallet_get_block_height(litewallet_server_uri: *const c_char) -> i64 {
    let litewallet_server_uri = unsafe { CStr::from_ptr(litewallet_server_uri) };
    match litewallet_server_uri.to_str() {
        Err(_) => -1,
        Ok(uri) => {
            let lightwalletd_uri = Uri::try_from(uri);
            match lightwalletd_uri {
                Err(_) => -2,
                Ok(uri) => match zingolib::get_latest_block_height(uri) {
                    Err(_) => -3,
                    Ok(height) => height as i64,
                },
            }
        }
    }
}

#[no_mangle]
pub extern "C" fn lightwallet_deinitialize() {
    LIGHTCLIENT.lock().unwrap().replace(None);
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
