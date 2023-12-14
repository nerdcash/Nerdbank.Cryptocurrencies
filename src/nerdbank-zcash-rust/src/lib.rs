uniffi::include_scaffolding!("ffi");

#[macro_use]
extern crate lazy_static;

mod backend_client;
mod error;
mod interop;
mod orchard;
mod sapling;

use interop::{
    BirthdayHeights, ChainType, LightWalletError, OrchardNote, SaplingNote, Transaction,
    TransactionSendDetail, UserBalances, WalletInfo,
};

use backend_client::sync;
