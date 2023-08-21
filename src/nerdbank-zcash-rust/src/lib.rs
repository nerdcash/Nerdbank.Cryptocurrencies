uniffi::include_scaffolding!("ffi");

#[macro_use]
extern crate lazy_static;

mod lightwallet;
mod orchard;
mod sapling;

use lightwallet::{
    lightwallet_deinitialize, lightwallet_get_block_height, lightwallet_initialize, Config,
    LightWalletError, Network,
};
