use std::{num::NonZeroU32, path::Path};

use http::Uri;
use nonempty::NonEmpty;
use rusqlite::Connection;
use zcash_address::ZcashAddress;
use zcash_client_backend::{
    data_api::{
        wallet::{
            create_proposed_transactions,
            input_selection::{GreedyInputSelector, GreedyInputSelectorError},
            propose_transfer,
        },
        Account, WalletRead,
    },
    fees::{zip317::SingleOutputChangeStrategy, ChangeStrategy},
    keys::UnifiedSpendingKey,
    proposal::Proposal,
    proto::service,
    wallet::OvkPolicy,
    zip321::{Payment, TransactionRequest},
    ShieldedProtocol,
};
use zcash_client_sqlite::{ReceivedNoteId, WalletDb};
use zcash_keys::keys::UnifiedFullViewingKey;
use zcash_primitives::{
    consensus::Network,
    memo::MemoBytes,
    transaction::{components::amount::NonNegativeAmount, fees::zip317::FeeRule, TxId},
};

use crate::{
    backing_store::Db, error::Error, grpc::get_client, interop::TransactionSendDetail,
    prover::get_prover,
};

#[derive(Debug)]
pub struct SendTransactionResult {
    pub txid: TxId,
}

pub(crate) fn create_send_proposal(
    db: &mut Db,
    network: Network,
    account_ufvk: &UnifiedFullViewingKey,
    min_confirmations: NonZeroU32,
    details: Vec<TransactionSendDetail>,
) -> Result<Proposal<FeeRule, ReceivedNoteId>, Error> {
    // TODO: revise this to a smarter change strategy that avoids unnecessarily crossing the turnstile.
    let input_selector = GreedyInputSelector::new(
        SingleOutputChangeStrategy::new(FeeRule::standard(), None, ShieldedProtocol::Sapling),
        Default::default(),
    );

    let mut payments = Vec::new();
    for detail in details.iter() {
        let memo = match &detail.memo {
            Some(m) => Some(MemoBytes::from_bytes(&m[..])?),
            None => None,
        };
        payments.push(
            Payment::new(
                ZcashAddress::try_from_encoded(detail.recipient.as_str())
                    .map_err(|_| Error::InvalidAddress)?,
                NonNegativeAmount::from_u64(detail.value).map_err(|_| Error::InvalidAmount)?,
                memo,
                None,
                None,
                Vec::new(),
            )
            .ok_or(Error::MemoNotAllowed)?,
        );
    }

    let request = TransactionRequest::new(payments)?;
    let account = db
        .data
        .get_account_for_ufvk(account_ufvk)?
        .ok_or(Error::KeyNotRecognized)?;

    Ok(propose_transfer::<_, _, _, Error>(
        &mut db.data,
        &network,
        account.id(),
        &input_selector,
        request,
        min_confirmations,
    )?)
}

pub async fn send_transaction<P: AsRef<Path>>(
    data_file: P,
    server_uri: Uri,
    network: Network,
    usk: &UnifiedSpendingKey,
    min_confirmations: NonZeroU32,
    details: Vec<TransactionSendDetail>,
) -> Result<NonEmpty<SendTransactionResult>, Error> {
    let mut db = Db::init(data_file, network)?;
    let proposal = create_send_proposal(
        &mut db,
        network,
        &usk.to_unified_full_viewing_key(),
        min_confirmations,
        details,
    )?;

    let prover = get_prover()?;
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

pub(crate) async fn transmit_transaction(
    txid: TxId,
    server_uri: Uri,
    db: &mut WalletDb<Connection, Network>,
) -> Result<SendTransactionResult, Error> {
    let mut client = get_client(server_uri).await?;
    let raw_tx = db
        .get_transaction(txid)?
        .ok_or(Error::Internal("Transaction not found".to_string()))
        .map(|tx| {
            let mut raw_tx = service::RawTransaction::default();
            tx.write(&mut raw_tx.data).unwrap();
            raw_tx
        })?;
    let response = client.send_transaction(raw_tx).await?.into_inner();
    if response.error_code != 0 {
        Err(Error::SendFailed {
            code: response.error_code,
            reason: response.error_message,
        })
    } else {
        Ok(SendTransactionResult { txid })
    }
}

#[cfg(test)]
mod tests {
    use matches::assert_matches;
    use tokio_util::sync::CancellationToken;

    use crate::{
        sync::sync,
        test_constants::{setup_test, MIN_CONFIRMATIONS, VALID_SAPLING_TESTNET},
    };

    use super::*;

    #[tokio_shared_rt::test]
    async fn test_send_insufficient_funds() {
        let mut setup = setup_test().await;
        let account = setup.create_account().await.unwrap();
        sync(
            setup.server_uri.clone(),
            setup.data_file.clone(),
            None,
            setup.db_init.min_confirmations,
            false,
            CancellationToken::new(),
        )
        .await
        .unwrap();
        let result = send_transaction(
            setup.data_file,
            setup.server_uri,
            setup.network,
            &account.3,
            NonZeroU32::try_from(MIN_CONFIRMATIONS).unwrap(),
            vec![TransactionSendDetail {
                value: 1000,
                memo: None,
                recipient: VALID_SAPLING_TESTNET.to_string(),
            }],
        )
        .await
        .unwrap_err();
        assert_matches!(result, Error::InsufficientFunds { .. });
    }
}
