use rusqlite::{Connection, named_params};
use zcash_client_sqlite::AccountUuid;

use zcash_client_backend::data_api::{WalletRead, wallet::ConfirmationsPolicy};
use zcash_primitives::transaction::fees::zip317::{FeeRule, MINIMUM_FEE};

use crate::{
    backing_store::Db,
    error::Error,
    interop::DbInit,
    sql_statements::{GET_BIRTHDAY_HEIGHTS, GET_UNSPENT_NOTES},
};

pub struct BirthdayHeights {
    /// The original birthday height given at account creation if non-zero,
    /// otherwise the block number of the first transaction if any,
    /// otherwise the sapling activation height.
    pub original_birthday_height: u32,
    /// The block number of the first transaction if any.
    pub birthday_height: Option<u32>,
    /// The block number of the oldest unspent note or UTXO, if any.
    pub rebirth_height: Option<u32>,
}

pub fn get_birthday_heights(
    config: DbInit,
    account_uuid: &AccountUuid,
) -> Result<BirthdayHeights, Error> {
    let conn = Connection::open(config.data_file)?;
    let heights = conn.query_row(
        GET_BIRTHDAY_HEIGHTS,
        named_params! {
            ":account_uuid": account_uuid.expose_uuid(),
        },
        |row| Ok((row.get(0)?, row.get::<_, Option<u32>>(1)?, row.get(2)?)),
    )?;

    Ok(BirthdayHeights {
        original_birthday_height: heights.0,
        birthday_height: heights.1,
        rebirth_height: heights.2,
    })
}

/// Balances that may be presented to a user in a wallet app.
/// The goal is to present a user-friendly and useful view of what the user has or can soon expect
/// *without* requiring the user to understand the details of the Zcash protocol.
///
/// Showing all these balances all the time may overwhelm the user with information.
/// A simpler view may present an overall balance as:
///
/// Name | Value
/// --- | ---
/// "Balance" | `spendable` - `minimum_fees` + `immature_change` + `immature_income`
/// "Incoming" | `incoming`
///
/// If dust is sent to the wallet, the simpler view's Incoming balance would include it,
/// only for it to evaporate when confirmed.
/// But incoming can always evaporate (e.g. a transaction expires before confirmation),
/// and the alternatives being to either hide that a transmission was made at all, or to include
/// the dust in other balances could be more misleading.
///
/// An app *could* choose to prominently warn the user if a significant proportion of the incoming balance is dust,
/// although this event seems very unlikely since it will cost the sender *more* than the amount the recipient is expecting
/// to 'fool' them into thinking they are receiving value.
/// The more likely scenario is that the sender is trying to send a small amount of value as a new user and doesn't realize
/// the value is too small to be useful.
/// A good Zcash wallet should prevent sending dust in the first place.
#[derive(Default)]
pub struct UserBalances {
    /// Available for immediate spending.
    /// Expected fees are *not* deducted from this value, but the app may do so by subtracting `minimum_fees`.
    /// `dust` is excluded from this value.
    ///
    /// For enhanced privacy, the minimum number of required confirmations to spend a note is usually greater than one.
    pub spendable: u64,

    /// The sum of the change notes that have insufficient confirmations to be spent.
    pub immature_change: u64,

    /// The minimum fees that can be expected to spend all `spendable + immature_change` funds in the wallet.
    /// This fee assumes all funds will be sent to a single note.
    ///
    /// Balances described by other fields in this struct are not included because they are not confirmed,
    /// they may amount to dust, or because as `immature_income` funds they may require shielding which has a cost
    /// and can change the amount of fees required to spend them (e.g. 3 UTXOs shielded together become only 1 note).
    pub minimum_fees: u64,

    /// The sum of non-change notes with a non-zero confirmation count that is less than the minimum required for spending,
    /// and all UTXOs (considering that UTXOs must be shielded before spending).
    /// `dust` is excluded from this value.
    ///
    /// As funds mature, this may not be the exact amount added to `spendable`, since the process of maturing
    /// may require shielding, which has a cost.
    pub immature_income: u64,

    /// The sum of all *confirmed* UTXOs and notes that are worth less than the fee to spend them,
    /// making them essentially inaccessible.
    pub dust: u64,

    /// The sum of all *unconfirmed* UTXOs and notes that are not change.
    /// This value includes any applicable `incoming_dust`.
    pub incoming: u64,

    /// The sum of all *unconfirmed* UTXOs and notes that are not change and are each counted as dust.
    pub incoming_dust: u64,
}


pub fn get_user_balances(
    config: &DbInit,
    account_uuid: &AccountUuid,
    confirmations_policy: ConfirmationsPolicy,
) -> Result<UserBalances, Error> {
    let marginal_fee: u64 = FeeRule::standard().marginal_fee().into();
    let db = Db::load(&config.data_file, config.network.into())?;
    let trusted_anchor = db
        .data
        .get_target_and_anchor_heights(confirmations_policy.trusted())?;
    let untrusted_anchor = db
        .data
        .get_target_and_anchor_heights(confirmations_policy.untrusted())?;
    if let Some((_, trusted_anchor)) = trusted_anchor
        && let Some((_, untrusted_anchor)) = untrusted_anchor
    {
        let conn = Connection::open(config.data_file.clone())?;
        let mut balances_query = conn.prepare(GET_UNSPENT_NOTES)?;
        let mut rows = balances_query.query(named_params! {
            ":account_uuid": account_uuid.expose_uuid(),
        })?;

        let mut balances = UserBalances {
            ..Default::default()
        };

        while let Some(row) = rows.next()? {
            let block_height: Option<u32> = row.get("block")?;
            let value: u64 = row.get("value")?;
            let output_pool: u8 = row.get("output_pool")?;
            let is_internal = row.get::<_, Option<i64>>("recipient_key_scope")? == Some(1i64); // KeyScope::INTERNAL
            let is_change: bool = row.get("is_change")?;

            let is_dust = value < marginal_fee;
            let is_shielded = output_pool > 1; // sprout is unspendable, but can be upgraded just like transparent.
            let relevant_anchor = if is_internal {
                trusted_anchor
            } else {
                untrusted_anchor
            };
            let is_mature = match block_height {
                Some(height) => height <= relevant_anchor.into(),
                None => false,
            };
            let is_spendable = is_mature && is_shielded;

            if !is_change && block_height.is_none() {
                balances.incoming += value;
                if is_dust {
                    balances.incoming_dust += value;
                }
            }

            if is_dust {
                if block_height.is_some() {
                    balances.dust += value;
                }
            } else {
                // The fee field only tracks mature income and change.
                if is_change || is_mature {
                    balances.minimum_fees += marginal_fee;
                }

                if is_spendable {
                    balances.spendable += value;
                } else if block_height.is_some() {
                    if is_change {
                        balances.immature_change += value;
                    } else {
                        balances.immature_income += value;
                    }
                } else {
                    // Unconfirmed
                }
            }
        }

        // Add the minimum fee for the receiving note,
        // but only if there exists notes to spend in the buckets that are covered by the minimum_fee.
        if balances.minimum_fees > 0 {
            balances.minimum_fees += marginal_fee; // The receiving note.

            if balances.minimum_fees < MINIMUM_FEE.into() {
                balances.minimum_fees = MINIMUM_FEE.into();
            }
        }

        // librustzcash is coming around to having more of these balances themselves.
        // For those values that it can provide, consider them more reliable and override what we computed.
        if let Some(summary) = db.data.get_wallet_summary(confirmations_policy)? {
            let account_balance = summary
                .account_balances()
                .get(account_uuid)
                .ok_or_else(|| Error::InvalidArgument("Account not found.".to_string()))?;
            balances.spendable = account_balance.spendable_value().into_u64();
            balances.immature_change = account_balance.change_pending_confirmation().into_u64();
            balances.dust = account_balance.uneconomic_value().into_u64();
        }

        Ok(balances)
    } else {
        Ok(Default::default())
    }
}

#[cfg(test)]
mod tests {
    use matches::assert_matches;

    use crate::test_constants::setup_test;

    use super::*;

    #[tokio_shared_rt::test(flavor = "multi_thread")]
    async fn test_get_birthday_heights() {
        let mut setup = setup_test().await;
        let (_, _, account_id, _) = setup.create_account().await.unwrap();
        let heights = get_birthday_heights(setup.db_init, &account_id).unwrap();
        assert_matches!(heights.birthday_height, None);
        assert_ne!(heights.original_birthday_height, 0);
        assert_matches!(heights.rebirth_height, None);
    }

    #[tokio_shared_rt::test(flavor = "multi_thread")]
    async fn test_get_user_balances() {
        let mut setup = setup_test().await;
        let (_, _, account_id, _) = setup.create_account().await.unwrap();
        setup.sync().await;

        let balances =
            get_user_balances(&setup.db_init, &account_id, ConfirmationsPolicy::default()).unwrap();
        assert_eq!(balances.spendable, 0);
    }
}
