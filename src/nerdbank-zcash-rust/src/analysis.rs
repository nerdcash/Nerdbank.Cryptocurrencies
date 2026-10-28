use rusqlite::{Connection, named_params};
use zcash_client_sqlite::AccountUuid;

use zcash_client_backend::data_api::{AccountBalance, WalletRead, wallet::ConfirmationsPolicy};
use zcash_primitives::transaction::builder::default_tx_expiry_delta;
use zcash_primitives::transaction::fees::zip317::{FeeRule, MINIMUM_FEE};
use zcash_protocol::consensus::{BranchId, Network, Parameters as _, UPGRADES_IN_ORDER};

use crate::{
    backing_store::Db,
    error::Error,
    interop::DbInit,
    sql_statements::{
        GET_BIRTHDAY_HEIGHTS, GET_UNSPENT_NOTES, get_unspent_transparent_by_mined_height_sql,
    },
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
/// "Balance" | `spendable` - `minimum_fees` + `immature_change` + `immature_income` + `viewed`
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

    /// Notes a view-only wallet decrypted and has not seen spent.
    ///
    /// A spending wallet reports zero. Those notes are in `spendable` or `immature_income`.
    /// A view-only wallet has no witness, so `spendable` stays zero and the observed total is
    /// here, including notes at or below the marginal fee.
    pub viewed: u64,
}

pub fn get_user_balances(
    config: &DbInit,
    account_uuid: &AccountUuid,
    confirmations_policy: ConfirmationsPolicy,
) -> Result<UserBalances, Error> {
    let marginal_fee: u64 = FeeRule::standard().marginal_fee().into();
    let db = Db::load(&config.data_file, config.network.into())?;
    let mut balances = UserBalances::default();
    // View-only shielded notes are reported from `viewed_value`. The legacy note query
    // classifies those same notes as spendable or immature and adds a spending fee, which
    // double-counts them once `viewed` is included. Transparent rows still use that query
    // only for a spending wallet. A view-only wallet classifies transparent outputs by
    // mined height after the shielded overlay, which does not need a shielded anchor.
    let view_only = !db.data.maintains_note_commitment_trees()?;
    let trusted_anchor = db
        .data
        .get_target_and_anchor_heights(confirmations_policy.trusted())?;
    let untrusted_anchor = db
        .data
        .get_target_and_anchor_heights(confirmations_policy.untrusted())?;
    if !view_only
        && let Some((_, trusted_anchor)) = trusted_anchor
        && let Some((_, untrusted_anchor)) = untrusted_anchor
    {
        let conn = Connection::open(config.data_file.clone())?;
        let mut balances_query = conn.prepare(GET_UNSPENT_NOTES)?;
        let mut rows = balances_query.query(named_params! {
            ":account_uuid": account_uuid.expose_uuid(),
        })?;

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
    }

    // Shielded anchors are absent for a wallet that never maintained note commitment trees.
    // The summary or snapshot still reports shielded `viewed_value`. A spending wallet prefers
    // the summary for the shielded split and keeps the legacy query's transparent figures.
    // A view-only wallet classifies transparent outputs from mined height directly, because the
    // library's pending and uneconomic buckets mix confirmed and unconfirmed funds.
    if let Some(summary) = db.data.get_wallet_summary(confirmations_policy)? {
        let account_balance = summary
            .account_balances()
            .get(account_uuid)
            .ok_or_else(|| Error::InvalidArgument("Account not found.".to_string()))?;
        overlay_library_balance(&mut balances, account_balance, view_only);
    } else if let Some(snapshot) = db.data.get_wallet_snapshot(confirmations_policy)?
        && let Some(account_balance) = snapshot.account_balances().get(account_uuid)
    {
        overlay_library_balance(&mut balances, account_balance, view_only);
    }
    if view_only {
        add_view_only_transparent_outputs(
            &config.data_file,
            config.network.into(),
            account_uuid,
            marginal_fee,
            &mut balances,
        )?;
    }

    Ok(balances)
}

/// Classifies unspent transparent outputs without a shielded anchor.
///
/// Mined outputs, including those still waiting for confirmations or coinbase maturity, count
/// toward the main balance (`immature_income`, or `dust` when they are below the marginal fee).
/// Unmined outputs count as incoming, and unmined dust also counts as `incoming_dust`.
fn add_view_only_transparent_outputs(
    data_file: &str,
    network: Network,
    account_uuid: &AccountUuid,
    marginal_fee: u64,
    balances: &mut UserBalances,
) -> Result<(), Error> {
    let conn = Connection::open(data_file)?;
    // `scan_queue` ends are exclusive, so the maximum end is the target height the
    // library passes to `spent_utxos_clause` (`chain_tip + 1`).
    let target_height: u32 = conn.query_row(
        "SELECT COALESCE(MAX(block_range_end), 1) FROM scan_queue",
        [],
        |row| row.get(0),
    )?;
    let sql = get_unspent_transparent_by_mined_height_sql(&expiry_delta_sql(&network));
    let mut query = conn.prepare(&sql)?;
    let mut rows = query.query(named_params! {
        ":account_uuid": account_uuid.expose_uuid(),
        ":target_height": target_height,
    })?;
    while let Some(row) = rows.next()? {
        let mined_height: Option<u32> = row.get(0)?;
        let value = u64::try_from(row.get::<_, i64>(1)?).map_err(|_| {
            Error::InvalidArgument("Transparent output value is negative.".to_string())
        })?;
        note_transparent_output(balances, mined_height, value, marginal_fee);
    }
    Ok(())
}

/// SQL expression for the default expiry delta at `transactions.min_observed_height`.
///
/// This follows `zcash_client_sqlite`'s `default_tx_expiry_delta_sql`: the delta changes
/// only when a network upgrade selects a different [`default_tx_expiry_delta`].
fn expiry_delta_sql(network: &Network) -> String {
    let mut activation_heights = UPGRADES_IN_ORDER
        .iter()
        .filter_map(|upgrade| network.activation_height(*upgrade))
        .collect::<Vec<_>>();
    activation_heights.sort();
    activation_heights.dedup();

    let mut current_delta = default_tx_expiry_delta(BranchId::Sprout);
    let mut delta_changes = Vec::new();
    for activation_height in activation_heights {
        let delta = default_tx_expiry_delta(BranchId::for_height(network, activation_height));
        if delta != current_delta {
            delta_changes.push((activation_height, delta));
            current_delta = delta;
        }
    }

    if delta_changes.is_empty() {
        return current_delta.to_string();
    }

    let arms = delta_changes
        .iter()
        .rev()
        .map(|(activation_height, delta)| {
            format!(
                "WHEN stx.min_observed_height >= {} THEN {delta}",
                u32::from(*activation_height)
            )
        })
        .collect::<Vec<_>>()
        .join(" ");
    let base = default_tx_expiry_delta(BranchId::Sprout);
    format!("(CASE {arms} ELSE {base} END)")
}

/// Adds one transparent output to the user-facing buckets.
fn note_transparent_output(
    balances: &mut UserBalances,
    mined_height: Option<u32>,
    value: u64,
    marginal_fee: u64,
) {
    let is_dust = value < marginal_fee;
    if mined_height.is_none() {
        balances.incoming += value;
        if is_dust {
            balances.incoming_dust += value;
        }
    } else if is_dust {
        balances.dust += value;
    } else {
        balances.immature_income += value;
    }
}

/// Copies shielded library balance buckets into the user-facing totals.
///
/// A spending wallet keeps the legacy query's incoming and immature-income figures and
/// replaces the shielded spendable, change, and dust totals with the summary. A view-only
/// wallet clears those spending buckets. Its shielded notes are only `viewed`. Transparent
/// outputs are added separately from mined height, not from these aggregate buckets.
fn overlay_library_balance(
    balances: &mut UserBalances,
    account_balance: &AccountBalance,
    view_only: bool,
) {
    if view_only {
        balances.spendable = 0;
        balances.minimum_fees = 0;
        balances.immature_change = 0;
        balances.immature_income = 0;
        balances.incoming = 0;
        balances.incoming_dust = 0;
        balances.dust = 0;
        balances.viewed = viewed_zatoshis(account_balance);
    } else {
        balances.spendable = account_balance.spendable_value().into_u64();
        balances.immature_change = account_balance.change_pending_confirmation().into_u64();
        balances.dust = account_balance.uneconomic_value().into_u64();
        balances.viewed = viewed_zatoshis(account_balance);
    }
}

/// Sum of [`Balance::viewed_value`](zcash_client_backend::data_api::Balance::viewed_value) across pools.
fn viewed_zatoshis(balance: &AccountBalance) -> u64 {
    (balance.sapling_balance().viewed_value()
        + balance.orchard_balance().viewed_value()
        + balance.ironwood_balance().viewed_value()
        + balance.unshielded_regular_balance().viewed_value()
        + balance.unshielded_coinbase_balance().viewed_value())
    .expect("viewed balance cannot overflow MAX_MONEY")
    .into_u64()
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
        assert_eq!(balances.viewed, 0);
    }

    #[test]
    fn view_only_overlay_keeps_shielded_notes_out_of_spending_buckets() {
        use zcash_protocol::value::{BalanceError, Zatoshis};

        let mut account = AccountBalance::ZERO;
        account
            .with_sapling_balance_mut::<(), BalanceError>(|balance| {
                balance.add_viewed_value(Zatoshis::from_u64(50_000_000).unwrap())?;
                Ok(())
            })
            .unwrap();
        let mut balances = UserBalances {
            spendable: 50_000_000,
            minimum_fees: 10_000,
            immature_income: 50_000_000,
            ..UserBalances::default()
        };
        overlay_library_balance(&mut balances, &account, true);
        let marginal_fee = 10_000;
        note_transparent_output(&mut balances, Some(1), 25_000_000, marginal_fee);
        note_transparent_output(&mut balances, Some(2), 1_000, marginal_fee);
        note_transparent_output(&mut balances, None, 4_000_000, marginal_fee);
        note_transparent_output(&mut balances, None, 500, marginal_fee);

        assert_eq!(balances.spendable, 0);
        assert_eq!(balances.minimum_fees, 0);
        assert_eq!(balances.viewed, 50_000_000);
        // Mined value, including outputs that have not reached the confirmation target.
        assert_eq!(balances.immature_income, 25_000_000);
        assert_eq!(balances.dust, 1_000);
        assert_eq!(balances.incoming, 4_000_500);
        assert_eq!(balances.incoming_dust, 500);
    }

    /// An unmined spend that has expired does not keep the transparent output out of the balance.
    /// A mined spend, and an unexpired unmined spend, still do.
    #[test]
    fn view_only_transparent_balance_ignores_expired_spends() {
        use zcash_client_backend::data_api::{
            Account as _, AccountBirthday, AccountPurpose, WalletWrite,
        };
        use zcash_client_backend::keys::UnifiedSpendingKey;
        use zcash_primitives::block::BlockHash;
        use zcash_protocol::consensus::BlockHeight;

        use super::Db;

        let network = Network::TestNetwork;
        let wallet_dir = testdir::testdir!();
        let mut db = Db::init(wallet_dir.join("wallet.sqlite"), network).unwrap();
        let birthday =
            AccountBirthday::from_viewing_parts(BlockHeight::from_u32(1), BlockHash([0; 32]), None);
        let ufvk = UnifiedSpendingKey::from_seed(&network, &[0x66; 32], zip32::AccountId::ZERO)
            .unwrap()
            .to_unified_full_viewing_key();
        let account = db
            .data
            .import_account_ufvk("view", &ufvk, &birthday, AccountPurpose::ViewOnly, None)
            .unwrap();

        let conn = rusqlite::Connection::open(&db.path).unwrap();
        conn.execute("DELETE FROM scan_queue", []).unwrap();
        conn.execute(
            "INSERT INTO scan_queue (block_range_start, block_range_end, priority) VALUES (1, 100, 10)",
            [],
        )
        .unwrap();
        let account_row: i64 = conn
            .query_row(
                "SELECT id FROM accounts WHERE uuid = ?1",
                [account.id().expose_uuid()],
                |row| row.get(0),
            )
            .unwrap();
        let (address_id, address): (i64, String) = conn
            .query_row(
                "SELECT id, address FROM addresses WHERE account_id = ?1 LIMIT 1",
                [account_row],
                |row| Ok((row.get(0)?, row.get(1)?)),
            )
            .unwrap();

        let mut txid = [0u8; 32];
        let mut insert_tx = |mined: Option<i64>, expiry: Option<i64>| {
            txid[0] = txid[0].wrapping_add(1);
            conn.query_row(
                "INSERT INTO transactions (txid, mined_height, expiry_height, min_observed_height)
                 VALUES (?1, ?2, ?3, 10) RETURNING id_tx",
                (txid.to_vec(), mined, expiry),
                |row| row.get(0),
            )
            .unwrap()
        };
        let insert_output = |tx_id: i64, index: i64, value: i64| {
            conn.query_row(
                "INSERT INTO transparent_received_outputs
                    (transaction_id, output_index, account_id, address, script, value_zat, address_id)
                 VALUES (?1, ?2, ?3, ?4, X'51', ?5, ?6)
                 RETURNING id",
                (tx_id, index, account_row, &address, value, address_id),
                |row| row.get(0),
            )
            .unwrap()
        };
        let link_spend = |output_id: i64, spend_tx: i64| {
            conn.execute(
                "INSERT INTO transparent_received_output_spends
                    (transparent_received_output_id, transaction_id) VALUES (?1, ?2)",
                (output_id, spend_tx),
            )
            .unwrap();
        };

        let received = insert_tx(Some(10), None);
        let expired_output = insert_output(received, 0, 25_000_000);
        let active_output = insert_output(received, 1, 8_000_000);
        let dust_output = insert_output(received, 2, 1_000);
        let unmined = insert_tx(None, Some(200));
        insert_output(unmined, 0, 4_000_000);

        link_spend(expired_output, insert_tx(None, Some(40)));
        link_spend(active_output, insert_tx(None, Some(150)));
        let _ = dust_output;

        drop(conn);
        let mut balances = UserBalances::default();
        add_view_only_transparent_outputs(
            db.path.to_str().unwrap(),
            network,
            &account.id(),
            10_000,
            &mut balances,
        )
        .unwrap();
        assert_eq!(balances.immature_income, 25_000_000);
        assert_eq!(balances.dust, 1_000);
        assert_eq!(balances.incoming, 4_000_000);
        assert_eq!(balances.incoming_dust, 0);
    }
}
