pub(crate) const GET_TRANSACTIONS_SQL: &str = r#"
	SELECT
		tx.id_tx,
		t.account_id,
		t.txid,
		t.mined_height,
		t.tx_index,
		t.expiry_height,
		t.account_balance_delta,
		t.fee_paid,
		t.block_time,
		t.expired_unmined,
		o.output_pool,
		o.from_account_id,
		o.to_account_id,
		o.to_address,
		s.diversifier,
		o.value,
		o.memo
	FROM v_transactions t
	LEFT OUTER JOIN v_tx_outputs o ON t.txid = o.txid
	LEFT OUTER JOIN transactions tx ON tx.txid = t.txid
	LEFT OUTER JOIN sapling_received_notes s ON txo.output_pool = 2 AND s.tx = tx.id_tx AND s.output_index = o.output_index
	WHERE (:account_id IS NULL OR t.account_id = :account_id) AND (t.mined_height IS NULL OR :starting_block IS NULL OR t.mined_height >= :starting_block)
	ORDER BY t.mined_height, t.tx_index
"#;

// TODO: update this to consider transparent (and eventually orchard) inputs.
// Note that WalletDb::get_min_unspent_height provides the rebirth height at the wallet level (instead of the account level).
pub(crate) const GET_BIRTHDAY_HEIGHTS: &str = r#"
	SELECT
		(SELECT birthday_height FROM accounts WHERE id = :account_id) AS "Original birthday height",
		(SELECT MIN(mined_height) FROM v_transactions WHERE account_id = :account_id) AS "Block with first note",
		(SELECT MIN(t.block)
			FROM sapling_received_notes s
			INNER JOIN transactions t ON s.tx = t.id_tx
			WHERE s.account_id = :account_id AND s.spent IS NULL
		) AS "Block with first unspent note"
"#;

// The v_tx_outputs view doesn't include transparent UTXOs, so we filter them out (for good measure) and add them via UNION with the utxos table.
pub(crate) const GET_UNSPENT_NOTES: &str = r#"
	SELECT
		tx.block,
		o.value,
		o.output_pool,
		COALESCE(o.from_account_id = o.to_account_id, 0) AS is_change
	FROM v_tx_outputs o
	INNER JOIN transactions tx ON tx.txid = o.txid
	LEFT OUTER JOIN sapling_received_notes s ON o.output_pool = 2 AND s.tx = tx.id_tx AND s.output_index = o.output_index
	WHERE o.to_account_id = :account_id AND s.spent IS NULL AND o.output_pool > 0

	UNION

	SELECT
		height,
		value_zat,
		0, -- output_pool
		0  -- is_change
	FROM utxos
	WHERE received_by_account_id = :account_id AND spent_in_tx IS NULL
"#;

pub(crate) const GET_UNSPENT_TRANSPARENT_NOTES: &str = r#"
	SELECT
		height,
		value_zat,
		address
	FROM utxos
	WHERE received_by_account_id = :account_id AND spent_in_tx IS NULL
	ORDER BY height
"#;
