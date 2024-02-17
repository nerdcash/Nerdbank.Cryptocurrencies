use std::path::Path;

use http::Uri;
use rusqlite::{named_params, Connection};
use zcash_client_backend::{
    data_api::wallet::{
        create_proposed_transaction,
        input_selection::{GreedyInputSelector, GreedyInputSelectorError},
        propose_shielding,
    },
    fees::{zip317::SingleOutputChangeStrategy, ChangeStrategy},
    keys::UnifiedSpendingKey,
    wallet::OvkPolicy,
};
use zcash_client_sqlite::ReceivedNoteId;
use zcash_primitives::{
    consensus::Network,
    legacy::TransparentAddress,
    transaction::fees::zip317::{FeeRule, MINIMUM_FEE},
    zip32::AccountId,
};
use zcash_proofs::prover::LocalTxProver;

use crate::{
    backing_store::Db,
    error::Error,
    interop::{DbInit, TransparentNote},
    send::{transmit_transaction, SendTransactionResult},
    sql_statements::GET_UNSPENT_TRANSPARENT_NOTES,
};

pub async fn shield_funds_at_address<P: AsRef<Path>>(
    data_file: P,
    server_uri: Uri,
    network: Network,
    usk: &UnifiedSpendingKey,
    address: TransparentAddress,
) -> Result<SendTransactionResult, Error> {
    let mut db = Db::init(data_file, network)?;

    // We want to be able to shield as soon as UTXOs appear in the mempool.
    let min_confirmations = 0;

    let prover = LocalTxProver::bundled();

    let input_selector = GreedyInputSelector::new(
        SingleOutputChangeStrategy::new(FeeRule::standard(), None),
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
    let txid = create_proposed_transaction::<
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

    transmit_transaction(txid, server_uri, &mut db.data).await
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
