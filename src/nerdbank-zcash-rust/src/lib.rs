uniffi::include_scaffolding!("ffi");

#[macro_use]
extern crate lazy_static;

mod analysis;
mod backend_client;
mod backing_store;
mod block_source;
mod error;
mod grpc;
mod interop;
mod lightclient;
mod orchard;
mod sapling;
mod sql_statements;

#[cfg(test)]
mod test_constants;

use analysis::{BirthdayHeights, UserBalances};
use interop::{
    lightwallet_disconnect_server, lightwallet_get_birthday_height,
    lightwallet_get_birthday_heights, lightwallet_get_block_height, lightwallet_get_sync_height,
    lightwallet_get_transactions, lightwallet_get_user_balances, lightwallet_init,
    lightwallet_sync, ChainType, DbInit, LightWalletError, ShieldedNote, SyncResult, Transaction,
    TransactionSendDetail, TransparentNote, WalletInfo,
};
