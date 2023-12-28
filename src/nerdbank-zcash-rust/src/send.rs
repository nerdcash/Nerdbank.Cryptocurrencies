use std::{num::NonZeroU32, path::Path};

use http::Uri;
use zcash_client_backend::{
    address::RecipientAddress,
    data_api::{
        wallet::{input_selection::GreedyInputSelector, spend},
        WalletRead,
    },
    fees::zip317::SingleOutputChangeStrategy,
    keys::UnifiedSpendingKey,
    proto::service,
    wallet::OvkPolicy,
    zip321::{Payment, TransactionRequest},
};
use zcash_primitives::{
    consensus::Network,
    memo::MemoBytes,
    transaction::{components::Amount, fees::zip317::FeeRule, Transaction, TxId},
};
use zcash_proofs::prover::LocalTxProver;

use crate::{backing_store::Db, error::Error, grpc::get_client, interop::TransactionSendDetail};

pub struct SendTransactionResult {
    pub txid: TxId,
    pub transaction: Transaction,
}

pub async fn send_transaction<P: AsRef<Path>>(
    data_file: P,
    server_uri: Uri,
    network: Network,
    usk: &UnifiedSpendingKey,
    min_confirmations: NonZeroU32,
    details: Vec<TransactionSendDetail>,
) -> Result<SendTransactionResult, Error> {
    let mut client = get_client(server_uri).await?;
    let mut db = Db::init(data_file, network)?;

    let prover = LocalTxProver::with_default_location()
        .ok_or(Error::InternalError("Missing parameters".to_string()))?;

    // TODO: revise this to a smarter change strategy that avoids unnecessarily crossing the turnstile.
    let input_selector = GreedyInputSelector::new(
        SingleOutputChangeStrategy::new(FeeRule::standard()),
        Default::default(),
    );

    let mut payments = Vec::new();
    for detail in details.iter() {
        let memo = match &detail.memo {
            Some(m) => Some(MemoBytes::from_bytes(&m[..])?),
            None => None,
        };
        payments.push(Payment {
            recipient_address: RecipientAddress::decode(&network, &detail.recipient.as_str())
                .ok_or(Error::InvalidAddress)?,
            amount: Amount::from_u64(detail.value).map_err(|_| Error::InvalidAmount)?,
            memo: memo,
            label: None,
            message: None,
            other_params: Vec::new(),
        });
    }

    let request = TransactionRequest::new(payments)?;

    let txid = spend(
        &mut db.data,
        &network,
        prover,
        &input_selector,
        usk,
        request,
        OvkPolicy::Sender,
        min_confirmations,
    )?;

    let (tx, raw_tx) = db.data.get_transaction(txid).map(|tx| {
        let mut raw_tx = service::RawTransaction::default();
        tx.write(&mut raw_tx.data).unwrap();
        (tx, raw_tx)
    })?;
    let response = client.send_transaction(raw_tx).await?.into_inner();
    if response.error_code != 0 {
        Err(Error::SendFailed {
            code: response.error_code,
            reason: response.error_message,
        })
    } else {
        Ok(SendTransactionResult {
            txid,
            transaction: tx,
        })
    }
}
