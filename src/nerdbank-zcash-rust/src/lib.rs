uniffi::include_scaffolding!("ffi");

#[macro_use]
extern crate lazy_static;

mod lightwallet;
mod orchard;
mod sapling;

use lightwallet::{
    last_synced_height, lightwallet_deinitialize, lightwallet_get_balances,
    lightwallet_get_birthday_height, lightwallet_get_block_height, lightwallet_get_transactions,
    lightwallet_initialize, lightwallet_initialize_from_disk, lightwallet_send_check_status,
    lightwallet_send_to_address, lightwallet_sync, lightwallet_sync_interrupt,
    lightwallet_sync_status, Config, LightWalletError, SendUpdate, SyncStatus, Transaction,
    TransactionSendDetail, WalletInfo,
};
use zingoconfig::ChainType;
use zingolib::lightclient::{PoolBalances, SyncResult};
