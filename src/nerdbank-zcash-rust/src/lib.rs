uniffi::include_scaffolding!("ffi");

#[macro_use]
extern crate lazy_static;

mod lightwallet;
mod orchard;
mod sapling;

use lightwallet::lightwallet_get_block_height;
