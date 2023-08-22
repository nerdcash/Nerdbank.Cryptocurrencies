uniffi::include_scaffolding!("ffi");

#[macro_use]
extern crate lazy_static;

mod lightwallet;
mod orchard;
mod sapling;

use lightwallet::{
    lightwallet_deinitialize, lightwallet_get_birthday_height, lightwallet_get_block_height,
    lightwallet_initialize, lightwallet_sync, lightwallet_sync_interrupt, lightwallet_sync_status,
    Config, LightWalletError, SyncStatus
};
use zingolib::lightclient::SyncResult;
use zingoconfig::ChainType;
