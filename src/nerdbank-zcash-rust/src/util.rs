use std::num::NonZeroUsize;

use zcash_client_backend::{
    data_api::wallet::input_selection::GreedyInputSelector,
    fees::{DustOutputPolicy, SplitPolicy, StandardFeeRule, zip317::MultiOutputChangeStrategy},
};
use zcash_protocol::{ShieldedPool, memo::MemoBytes, value::Zatoshis};

pub fn zip317_helper<DbT>(
    change_memo: Option<MemoBytes>,
) -> (
    MultiOutputChangeStrategy<StandardFeeRule, DbT>,
    GreedyInputSelector<DbT>,
) {
    // TODO: revise this to a smarter change strategy that avoids unnecessarily crossing the turnstile.
    (
        MultiOutputChangeStrategy::new(
            StandardFeeRule::Zip317,
            change_memo,
            ShieldedPool::Orchard,
            DustOutputPolicy::default(),
            SplitPolicy::with_min_output_value(
                NonZeroUsize::new(4).expect("4 is nonzero"),
                Zatoshis::const_from_u64(1000_0000),
            ),
        ),
        GreedyInputSelector::new(),
    )
}

/// Decodes a recorded shielded recipient key scope, rejecting missing or unknown scopes.
pub(crate) fn parse_recipient_key_scope(
    code: Option<i64>,
) -> Result<zip32::Scope, crate::error::Error> {
    match code {
        Some(0) => Ok(zip32::Scope::External),
        Some(1) => Ok(zip32::Scope::Internal),
        _ => Err(crate::error::Error::SqliteClient(
            zcash_client_sqlite::error::SqliteClientError::CorruptedData(format!(
                "Unknown recipient key scope: {code:?}"
            )),
        )),
    }
}
