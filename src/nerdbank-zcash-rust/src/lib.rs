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
    add_account, add_diversifier, disconnect_server, get_accounts, get_birthday_height,
    get_birthday_heights, get_block_height, get_sync_height, get_transactions,
    get_unshielded_utxos, get_user_balances, init, send, shield, sync, AccountInfo, ChainType,
    DbInit, LightWalletError, SendTransactionResult, ShieldedNote, SyncResult, Transaction,
    TransactionSendDetail, TransparentNote,
};
