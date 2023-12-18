uniffi::include_scaffolding!("ffi");

#[macro_use]
extern crate lazy_static;

mod backend_client;
mod backing_store;
mod block_source;
mod error;
mod grpc;
mod interop;
mod lightclient;
mod orchard;
mod sapling;

#[cfg(test)]
mod test_constants;

use interop::{
    lightwallet_disconnect_server, lightwallet_get_block_height, lightwallet_init,
    lightwallet_sync, BirthdayHeights, ChainType, LightWalletError, OrchardNote, SaplingNote,
    SyncResult, Transaction, TransactionSendDetail, UserBalances, WalletInfo,
};
