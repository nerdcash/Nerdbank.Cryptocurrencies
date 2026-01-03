use std::{convert::Infallible, num::NonZeroU32, path::Path};

use crate::interop::{DbInit, TransparentNote};
use crate::sql_statements::GET_UNSPENT_TRANSPARENT_NOTES;
use crate::util::zip317_helper;
use crate::{
    backing_store::Db,
    error::Error,
    prover::get_prover,
    send::{SendTransactionResult, transmit_transaction},
};
use http::Uri;
use nonempty::NonEmpty;
use rusqlite::{Connection, named_params};
use zcash_client_backend::data_api::WalletRead;
use zcash_client_backend::{
    data_api::wallet::{self, create_proposed_transactions, propose_shielding},
    keys::UnifiedSpendingKey,
    wallet::OvkPolicy,
};
use zcash_client_sqlite::AccountUuid;
use zcash_protocol::memo::MemoBytes;
use zcash_protocol::{consensus::Network, value::Zatoshis};
use zcash_transparent::address::TransparentAddress;
use zcash_transparent::keys::TransparentKeyScope;

pub async fn shield_funds<P: AsRef<Path>>(
    data_file: P,
    server_uri: Uri,
    network: Network,
    account_uuid: AccountUuid,
    usk: &UnifiedSpendingKey,
    shielding_threshold: Zatoshis,
    address: Option<TransparentAddress>,
    memo: Option<MemoBytes>,
) -> Result<NonEmpty<SendTransactionResult>, Error> {
    let mut db = Db::init(data_file, network)?;

    // We want to be able to shield as soon as UTXOs appear in the mempool.
    let confirmations_policy = wallet::ConfirmationsPolicy::MIN;

    let account_receivers = db
        .data
        .get_target_and_anchor_heights(NonZeroU32::MIN)
        .map_err(|e| Error::from(e))
        .and_then(|opt| {
            opt.map(|(target, _)| target) // Include unconfirmed funds.
                .ok_or_else(|| Error::SyncFirst)
        })
        .and_then(|target_height| {
            db.data
                .get_transparent_balances(account_uuid, target_height, confirmations_policy)
                .map_err(|e| Error::from(e))
        })?;

    // If a specific address is specified, or balance only exists for one address, select the
    // value for that address.
    //
    // Otherwise, if there are any non-ephemeral addresses, select value for all those
    // addresses. See the warnings associated with the documentation of the
    // `transparent_receiver` argument in the method documentation for privacy considerations.
    //
    // Finally, if there are only ephemeral addresses, select value for exactly one of those
    // addresses.
    let from_addrs: Vec<TransparentAddress> = match address {
        Some(addr) => account_receivers
            .get(&addr)
            .and_then(|(_, balance)| {
                (balance.spendable_value() >= shielding_threshold).then_some(addr)
            })
            .into_iter()
            .collect(),
        None => {
            let (ephemeral, non_ephemeral): (Vec<_>, Vec<_>) = account_receivers
                .into_iter()
                .filter(|(_, (_, balance))| balance.spendable_value() >= shielding_threshold)
                .partition(|(_, (scope, _))| *scope == TransparentKeyScope::EPHEMERAL);

            if non_ephemeral.is_empty() {
                ephemeral
                    .into_iter()
                    .take(1)
                    .map(|(addr, _)| addr)
                    .collect()
            } else {
                non_ephemeral.into_iter().map(|(addr, _)| addr).collect()
            }
        }
    };

    let mut result = Vec::new();

    if from_addrs.is_empty() {
        // There are no transparent funds to shield; don't create a proposal.
        return Ok(NonEmpty::from_vec(result).unwrap());
    };

    let (change_strategy, input_selector) = zip317_helper(memo);

    let proposal = propose_shielding::<_, _, _, _, Infallible>(
        &mut db.data,
        &network,
        &input_selector,
        &change_strategy,
        shielding_threshold,
        &from_addrs,
        account_uuid,
        confirmations_policy,
    )?;

    let prover = get_prover()?;

    let txids = create_proposed_transactions::<_, _, Infallible, _, Infallible, _>(
        &mut db.data,
        &network,
        &prover,
        &prover,
        &wallet::SpendingKeys::from_unified_spending_key(usk.to_owned()),
        OvkPolicy::Sender,
        &proposal,
    )?;

    for txid in txids {
        result.push(transmit_transaction(txid, server_uri.clone(), &mut db.data).await?);
    }

    Ok(NonEmpty::from_vec(result).unwrap())
}

/// Returns a list of unshielded UTXOs for the given account,
/// sorted by height (ascending).
pub fn get_unshielded_utxos(
    config: DbInit,
    account_id: AccountUuid,
) -> Result<Vec<TransparentNote>, Error> {
    let conn = Connection::open(config.data_file)?;
    let mut balances_query = conn.prepare(GET_UNSPENT_TRANSPARENT_NOTES)?;
    let mut rows = balances_query.query(named_params! {
        ":account_uuid": account_id.expose_uuid(),
    })?;

    let mut utxos = vec![];
    while let Some(row) = rows.next()? {
        let zats: u64 = row.get("value_zat")?;
        let address: String = row.get("address")?;
        utxos.push(TransparentNote {
            value: zats,
            recipient: address,
        })
    }

    Ok(utxos)
}

#[cfg(test)]
mod tests {
    use crate::test_constants::setup_test;

    use super::*;

    #[tokio_shared_rt::test(flavor = "multi_thread")]
    async fn test_get_unshielded_utxos() {
        let mut setup = setup_test().await;
        let (_, _, account_id, _) = setup.create_account().await.unwrap();
        setup.sync().await;

        let utxos = get_unshielded_utxos(setup.db_init, account_id).unwrap();
        assert_eq!(0, utxos.len());
    }
}
