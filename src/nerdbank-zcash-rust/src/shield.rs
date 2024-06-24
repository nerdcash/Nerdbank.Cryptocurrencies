use std::path::Path;

use http::Uri;
use nonempty::NonEmpty;
use rusqlite::{named_params, Connection};
use zcash_client_backend::{
    data_api::wallet::{
        create_proposed_transactions,
        input_selection::{GreedyInputSelector, GreedyInputSelectorError},
        propose_shielding,
    },
    fees::{zip317::SingleOutputChangeStrategy, ChangeStrategy},
    keys::UnifiedSpendingKey,
    wallet::OvkPolicy,
    ShieldedProtocol,
};
use zcash_client_sqlite::{AccountId, ReceivedNoteId};
use zcash_primitives::{
    consensus::Network,
    legacy::TransparentAddress,
    transaction::fees::zip317::{FeeRule, MINIMUM_FEE},
};

use crate::{
    backing_store::Db,
    error::Error,
    interop::{DbInit, TransparentNote},
    prover::get_prover,
    send::{transmit_transaction, SendTransactionResult},
    sql_statements::GET_UNSPENT_TRANSPARENT_NOTES,
};

pub async fn shield_funds_at_address<P: AsRef<Path>>(
    data_file: P,
    server_uri: Uri,
    network: Network,
    usk: &UnifiedSpendingKey,
    address: TransparentAddress,
) -> Result<NonEmpty<SendTransactionResult>, Error> {
    let mut db = Db::init(data_file, network)?;

    // We want to be able to shield as soon as UTXOs appear in the mempool.
    let min_confirmations = 0;

    let prover = get_prover()?;
    let input_selector = GreedyInputSelector::new(
        SingleOutputChangeStrategy::new(FeeRule::standard(), None, ShieldedProtocol::Sapling),
        Default::default(),
    );
    let proposal = propose_shielding::<_, _, _, zcash_client_sqlite::wallet::commitment_tree::Error>(
        &mut db.data,
        &network,
        &input_selector,
        MINIMUM_FEE,
        &[address],
        min_confirmations,
    )?;
    let txids = create_proposed_transactions::<
        _,
        _,
        GreedyInputSelectorError<
            <SingleOutputChangeStrategy as ChangeStrategy>::Error,
            ReceivedNoteId,
        >,
        _,
        _,
    >(
        &mut db.data,
        &network,
        &prover,
        &prover,
        usk,
        OvkPolicy::Sender,
        &proposal,
    )?;

    let mut result = Vec::new();
    for txid in txids {
        result.push(transmit_transaction(txid, server_uri.clone(), &mut db.data).await?);
    }

    Ok(NonEmpty::from_vec(result).unwrap())
}

/// Returns a list of unshielded UTXOs for the given account,
/// sorted by height (ascending).
pub fn get_unshielded_utxos(
    config: DbInit,
    account_id: AccountId,
) -> Result<Vec<TransparentNote>, Error> {
    let conn = Connection::open(config.data_file)?;
    let mut balances_query = conn.prepare(GET_UNSPENT_TRANSPARENT_NOTES)?;
    let mut rows = balances_query.query(named_params! {
        ":account_id": u32::from(account_id),
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
