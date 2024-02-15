uniffi::include_scaffolding!("ffi");

#[macro_use]
extern crate lazy_static;

mod analysis;
mod backing_store;
mod block_source;
mod error;
mod grpc;
mod interop;
mod lightclient;
mod orchard;
mod resilience;
mod sapling;
mod send;
mod shield;
mod sql_statements;
mod sync;

#[cfg(test)]
mod test_constants;

use analysis::{BirthdayHeights, UserBalances};
use interop::{
    get_accounts, lightwallet_add_account, lightwallet_add_diversifier,
    lightwallet_disconnect_server, lightwallet_get_birthday_height,
    lightwallet_get_birthday_heights, lightwallet_get_block_height, lightwallet_get_sync_height,
    lightwallet_get_transactions, lightwallet_get_unshielded_utxos, lightwallet_get_user_balances,
    lightwallet_init, lightwallet_send, lightwallet_shield, lightwallet_sync, AccountInfo,
    ChainType, DbInit, LightWalletError, SendTransactionResult, ShieldedNote, SyncResult,
    Transaction, TransactionSendDetail, TransparentNote,
};
