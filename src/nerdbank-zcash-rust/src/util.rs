use std::num::NonZeroUsize;

use zcash_client_backend::{
    data_api::wallet::input_selection::GreedyInputSelector,
    fees::{DustOutputPolicy, SplitPolicy, StandardFeeRule, zip317::MultiOutputChangeStrategy},
};
use zcash_protocol::{ShieldedProtocol, memo::MemoBytes, value::Zatoshis};

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
            ShieldedProtocol::Orchard,
            DustOutputPolicy::default(),
            SplitPolicy::with_min_output_value(
                NonZeroUsize::new(4).expect("4 is nonzero"),
                Zatoshis::const_from_u64(1000_0000),
            ),
        ),
        GreedyInputSelector::new(),
    )
}
