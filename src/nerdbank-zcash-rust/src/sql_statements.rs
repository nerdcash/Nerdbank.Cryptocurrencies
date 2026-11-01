pub(crate) const GET_TRANSACTIONS_SQL: &str = r#"
	SELECT
		tx.id_tx,
		t.account_uuid,
		t.txid,
		t.mined_height,
		t.tx_index,
		t.expiry_height,
		t.account_balance_delta,
		t.fee_paid,
		t.block_time,
		t.expired_unmined,
		txo.output_pool,
		coalesce(
			txo.from_account_uuid,
			(SELECT a.uuid
			 FROM sapling_received_notes rn
			 JOIN accounts a ON a.id = rn.account_id
			 JOIN sapling_received_note_spends spends ON spends.sapling_received_note_id = rn.id
			 WHERE spends.transaction_id = tx.id_tx
			 LIMIT 1),
			(SELECT a.uuid
			 FROM orchard_received_notes rn
			 JOIN accounts a ON a.id = rn.account_id
			 JOIN orchard_received_note_spends spends ON spends.orchard_received_note_id = rn.id
			 WHERE spends.transaction_id = tx.id_tx
			 LIMIT 1),
			(SELECT a.uuid
			 FROM ironwood_received_notes rn
			 JOIN accounts a ON a.id = rn.account_id
			 JOIN ironwood_received_note_spends spends ON spends.ironwood_received_note_id = rn.id
			 WHERE spends.transaction_id = tx.id_tx
			 LIMIT 1)
		) AS from_account_uuid,
		txo.to_account_uuid,
		coalesce(
			(SELECT to_address FROM v_tx_outputs vtxo WHERE vtxo.txid = t.txid AND vtxo.output_pool = txo.output_pool AND vtxo.output_index = txo.output_index AND to_address IS NOT NULL),
			(SELECT address FROM transparent_received_outputs txo WHERE txo.transaction_id = tx.id_tx AND txo.output_index = txo.output_index AND address IS NOT NULL)
		) AS to_address,
		coalesce(s.diversifier, o.diversifier, i.diversifier) AS diversifier,
		coalesce(s.recipient_key_scope, o.recipient_key_scope, i.recipient_key_scope) AS recipient_key_scope,
		txo.value,
		txo.memo
	FROM v_transactions t
	LEFT OUTER JOIN v_tx_outputs txo ON t.txid = txo.txid
	LEFT OUTER JOIN transactions tx ON tx.txid = t.txid
	LEFT OUTER JOIN sapling_received_notes s ON txo.output_pool = 2 AND s.transaction_id = tx.id_tx AND s.output_index = txo.output_index
	LEFT OUTER JOIN orchard_received_notes o ON txo.output_pool = 3 AND o.transaction_id = tx.id_tx AND o.action_index = txo.output_index
	LEFT OUTER JOIN ironwood_received_notes i ON txo.output_pool = 4 AND i.transaction_id = tx.id_tx AND i.action_index = txo.output_index
	WHERE (:account_uuid IS NULL OR t.account_uuid = :account_uuid)
		AND (from_account_uuid = t.account_uuid OR txo.to_account_uuid = t.account_uuid)
		AND (from_account_uuid IS NOT NULL OR to_account_uuid IS NOT NULL) -- ignore transactions that probably aren't fully initialized
		AND (t.mined_height IS NULL OR :starting_block IS NULL OR t.mined_height >= :starting_block)
		AND (t.mined_height IS NULL OR :ending_block IS NULL OR t.mined_height <= :ending_block)
	GROUP BY t.account_uuid, tx.id_tx, t.account_uuid, txo.output_pool, txo.output_index
	ORDER BY t.account_uuid, t.mined_height, t.tx_index, txo.output_pool, txo.output_index -- ensure rows that get squashed together are next to each other
"#;

// TODO: update this to consider UTXOs in "Block with first unspent note" column.
// Note that WalletDb::get_min_unspent_height provides the rebirth height at the wallet level (instead of the account level).
/// Returns birthday-height SQL, treating only mined or unexpired transactions as spends.
/// The caller binds `:account_uuid` and the exclusive chain-tip `:target_height`.
pub(crate) fn get_birthday_heights_sql(expiry_delta_sql: &str) -> String {
    let unexpired_spend = unexpired_spend_condition(expiry_delta_sql);
    format!(
        r#"
	WITH params(account_id) AS (
		SELECT id FROM accounts WHERE uuid = :account_uuid LIMIT 1
	), unexpired_spends(transaction_id) AS (
		SELECT stx.id_tx FROM transactions stx WHERE {unexpired_spend}
	)
	SELECT
		(SELECT birthday_height FROM accounts WHERE uuid = :account_uuid) AS "Original birthday height",
		(SELECT MIN(mined_height) FROM v_transactions WHERE account_uuid = :account_uuid) AS "Block with first note",
		(SELECT MIN(block) FROM (
			SELECT t.block
			FROM sapling_received_notes s
			JOIN transactions t ON t.id_tx = s.transaction_id
			LEFT OUTER JOIN sapling_received_note_spends ss ON ss.sapling_received_note_id = s.id AND ss.transaction_id IN (SELECT transaction_id FROM unexpired_spends)
			WHERE s.account_id = (SELECT account_id FROM params) AND ss.transaction_id IS NULL
			UNION ALL
			SELECT t.block
			FROM orchard_received_notes o
			JOIN transactions t ON t.id_tx = o.transaction_id
			LEFT OUTER JOIN orchard_received_note_spends os ON os.orchard_received_note_id = o.id AND os.transaction_id IN (SELECT transaction_id FROM unexpired_spends)
			WHERE o.account_id = (SELECT account_id FROM params) AND os.transaction_id IS NULL
			UNION ALL
			SELECT t.block
			FROM ironwood_received_notes i
			JOIN transactions t ON t.id_tx = i.transaction_id
			LEFT OUTER JOIN ironwood_received_note_spends isp ON isp.ironwood_received_note_id = i.id AND isp.transaction_id IN (SELECT transaction_id FROM unexpired_spends)
			WHERE i.account_id = (SELECT account_id FROM params) AND isp.transaction_id IS NULL
		)) AS "Block with first unspent note"
"#
    )
}

// The v_tx_outputs view doesn't include transparent UTXOs, so we filter them out (for good measure) and add them via UNION with the utxos table.
/// Returns unspent-note SQL, ignoring spends that expired without being mined.
/// The caller binds `:account_uuid` and the exclusive chain-tip `:target_height`.
pub(crate) fn get_unspent_notes_sql(expiry_delta_sql: &str) -> String {
    let unexpired_spend = unexpired_spend_condition(expiry_delta_sql);
    format!(
        r#"
	WITH params(account_id) AS (
		SELECT id FROM accounts WHERE uuid = :account_uuid LIMIT 1
	), unexpired_spends(transaction_id) AS (
		SELECT stx.id_tx FROM transactions stx WHERE {unexpired_spend}
	)

	SELECT
		tx.block,
		txo.value,
		txo.output_pool,
		coalesce(s.recipient_key_scope, o.recipient_key_scope, i.recipient_key_scope) AS recipient_key_scope,
		COALESCE(txo.from_account_uuid = txo.to_account_uuid, 0) AS is_change
	FROM v_tx_outputs txo
	INNER JOIN transactions tx ON tx.txid = txo.txid
	LEFT OUTER JOIN sapling_received_notes s ON txo.output_pool = 2 AND s.transaction_id = tx.id_tx AND s.output_index = txo.output_index
	LEFT OUTER JOIN sapling_received_note_spends ss ON ss.sapling_received_note_id = s.id AND ss.transaction_id IN (SELECT transaction_id FROM unexpired_spends)
	LEFT OUTER JOIN orchard_received_notes o ON txo.output_pool = 3 AND o.transaction_id = tx.id_tx AND o.action_index = txo.output_index
	LEFT OUTER JOIN orchard_received_note_spends os ON os.orchard_received_note_id = o.id AND os.transaction_id IN (SELECT transaction_id FROM unexpired_spends)
	LEFT OUTER JOIN ironwood_received_notes i ON txo.output_pool = 4 AND i.transaction_id = tx.id_tx AND i.action_index = txo.output_index
	LEFT OUTER JOIN ironwood_received_note_spends isp ON isp.ironwood_received_note_id = i.id AND isp.transaction_id IN (SELECT transaction_id FROM unexpired_spends)
	WHERE txo.to_account_uuid = :account_uuid AND ss.transaction_id IS NULL AND os.transaction_id IS NULL AND isp.transaction_id IS NULL AND txo.output_pool > 0

	UNION ALL

	SELECT
		t.block,
		value_zat,
		0, -- output_pool
		0, -- recipient_key_scope
		0  -- is_change
	FROM transparent_received_outputs txo
	INNER JOIN transactions t ON t.id_tx = txo.transaction_id
	LEFT OUTER JOIN transparent_received_output_spends j ON txo.id = j.transparent_received_output_id AND j.transaction_id IN (SELECT transaction_id FROM unexpired_spends)
	WHERE account_id = (SELECT account_id FROM params) AND j.transaction_id IS NULL
"#
    )
}

/// Unspent transparent outputs for one account, with a null mined height when the
/// creating transaction is still unconfirmed.
///
/// A spend row is ignored once its spending transaction has expired without being mined.
/// `expiry_delta_sql` is the library's default-expiry expression for `stx.min_observed_height`.
/// The caller binds `:account_uuid` and `:target_height` (the exclusive end of the wallet's
/// chain tip, matching `spent_utxos_clause`).
pub(crate) fn get_unspent_transparent_by_mined_height_sql(expiry_delta_sql: &str) -> String {
    let unexpired_spend = unexpired_spend_condition(expiry_delta_sql);
    format!(
        r#"
	SELECT t.mined_height, txo.value_zat
	FROM transparent_received_outputs txo
	INNER JOIN transactions t ON t.id_tx = txo.transaction_id
	INNER JOIN accounts a ON a.id = txo.account_id
	WHERE a.uuid = :account_uuid
	  AND txo.id NOT IN (
		SELECT spent.transparent_received_output_id
		FROM transparent_received_output_spends spent
		JOIN transactions stx ON stx.id_tx = spent.transaction_id
		WHERE {unexpired_spend}
	  )
"#
    )
}

/// Matches librustzcash's mined-or-unexpired transaction condition at `:target_height`.
fn unexpired_spend_condition(expiry_delta_sql: &str) -> String {
    format!(
        r#"
		stx.mined_height < :target_height
		OR stx.expiry_height = 0
		OR stx.expiry_height >= :target_height
		OR (
			stx.expiry_height IS NULL
			AND stx.min_observed_height + {expiry_delta_sql} >= :target_height
		)
	"#
    )
}

pub(crate) const GET_UNSPENT_TRANSPARENT_NOTES: &str = r#"
	WITH params(account_id) AS (
		SELECT id FROM accounts WHERE uuid = :account_uuid LIMIT 1
	)
	SELECT
		t.block AS height,
		value_zat,
		address
	FROM transparent_received_outputs txo
	INNER JOIN transactions t ON t.id_tx = txo.transaction_id
	LEFT OUTER JOIN transparent_received_output_spends j ON j.transparent_received_output_id = txo.id
	WHERE account_id = (SELECT account_id FROM params) AND j.transaction_id IS NULL
	ORDER BY t.block
"#;

pub(crate) const GET_OUTPOINT_VALUE: &str = r#"
	SELECT value_zat
	FROM transparent_received_outputs txo
	INNER JOIN transactions t ON txo.transaction_id = t.id_tx
	WHERE t.txid = :txid AND output_index = :idx
"#;

/// Returns transactions containing incoming payments filtered by account and receiver.
/// For shielded pools (Orchard/Ironwood/Sapling), filters by pool, diversifier, and key scope.
/// For transparent pools, filters by the address string.
pub(crate) const GET_INCOMING_PAYMENTS_SQL: &str = r#"
	SELECT
		tx.id_tx,
		t.account_uuid,
		t.txid,
		t.mined_height,
		t.tx_index,
		t.expiry_height,
		t.account_balance_delta,
		t.fee_paid,
		t.block_time,
		t.expired_unmined,
		txo.output_pool,
		coalesce(
			txo.from_account_uuid,
			(SELECT a.uuid
			 FROM sapling_received_notes rn
			 JOIN accounts a ON a.id = rn.account_id
			 JOIN sapling_received_note_spends spends ON spends.sapling_received_note_id = rn.id
			 WHERE spends.transaction_id = tx.id_tx
			 LIMIT 1),
			(SELECT a.uuid
			 FROM orchard_received_notes rn
			 JOIN accounts a ON a.id = rn.account_id
			 JOIN orchard_received_note_spends spends ON spends.orchard_received_note_id = rn.id
			 WHERE spends.transaction_id = tx.id_tx
			 LIMIT 1),
			(SELECT a.uuid
			 FROM ironwood_received_notes rn
			 JOIN accounts a ON a.id = rn.account_id
			 JOIN ironwood_received_note_spends spends ON spends.ironwood_received_note_id = rn.id
			 WHERE spends.transaction_id = tx.id_tx
			 LIMIT 1)
		) AS from_account_uuid,
		txo.to_account_uuid,
		coalesce(
			(SELECT to_address FROM v_tx_outputs vtxo WHERE vtxo.txid = t.txid AND vtxo.output_pool = txo.output_pool AND vtxo.output_index = txo.output_index AND to_address IS NOT NULL),
			(SELECT address FROM transparent_received_outputs tro WHERE tro.transaction_id = tx.id_tx AND tro.output_index = txo.output_index AND address IS NOT NULL)
		) AS to_address,
		coalesce(s.diversifier, o.diversifier, i.diversifier) AS diversifier,
		coalesce(s.recipient_key_scope, o.recipient_key_scope, i.recipient_key_scope) AS recipient_key_scope,
		txo.value,
		txo.memo
	FROM v_transactions t
	LEFT OUTER JOIN v_tx_outputs txo ON t.txid = txo.txid
	LEFT OUTER JOIN transactions tx ON tx.txid = t.txid
	LEFT OUTER JOIN sapling_received_notes s ON txo.output_pool = 2 AND s.transaction_id = tx.id_tx AND s.output_index = txo.output_index
	LEFT OUTER JOIN orchard_received_notes o ON txo.output_pool = 3 AND o.transaction_id = tx.id_tx AND o.action_index = txo.output_index
	LEFT OUTER JOIN ironwood_received_notes i ON txo.output_pool = 4 AND i.transaction_id = tx.id_tx AND i.action_index = txo.output_index
	LEFT OUTER JOIN transparent_received_outputs tro ON txo.output_pool = 0 AND tro.transaction_id = tx.id_tx AND tro.output_index = txo.output_index
	WHERE t.account_uuid = :account_uuid
		AND txo.to_account_uuid = t.account_uuid
		AND (from_account_uuid IS NOT NULL OR to_account_uuid IS NOT NULL)
		AND (t.mined_height IS NULL OR :starting_block IS NULL OR t.mined_height >= :starting_block)
		AND (
			-- Orchard and Ironwood share a receiver; Sapling uses a distinct receiver.
			(txo.output_pool IN (2, 3, 4)
			 AND (txo.output_pool = :output_pool OR (:output_pool = 3 AND txo.output_pool = 4))
			 AND coalesce(s.diversifier, o.diversifier, i.diversifier) = :diversifier
			 AND coalesce(s.recipient_key_scope, o.recipient_key_scope, i.recipient_key_scope) = :recipient_key_scope)
			-- For transparent pool, match by address string directly from transparent_received_outputs
			OR (txo.output_pool = 0 AND :transparent_address IS NOT NULL AND tro.address = :transparent_address)
		)
	GROUP BY t.account_uuid, tx.id_tx, t.account_uuid, txo.output_pool, txo.output_index
	ORDER BY t.account_uuid, t.mined_height, t.tx_index, txo.output_pool, txo.output_index
"#;
