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
		o.from_account,
		o.to_account,
		o.to_address,
		s.diversifier,
		o.value,
		o.memo
	FROM v_transactions t
	LEFT OUTER JOIN v_tx_outputs o ON t.txid = o.txid
	LEFT OUTER JOIN transactions tx ON tx.txid = t.txid
	LEFT OUTER JOIN sapling_received_notes s ON tx.id_tx = s.tx
	ORDER BY t.mined_height, t.tx_index
"#;

// TODO: update this to consider transparent (and eventually orchard) inputs.
pub(crate) const GET_BIRTHDAY_HEIGHTS: &str = r#"
	SELECT
		(SELECT birthday_height FROM accounts WHERE account = :account_id) AS "Original birthday height",
		(SELECT MIN(height) FROM blocks WHERE sapling_commitment_tree_size >= (SELECT MIN(commitment_tree_position) FROM sapling_received_notes WHERE account = :account_id)) AS "Block with first note",
		(SELECT MIN(height) FROM blocks WHERE sapling_commitment_tree_size >= (SELECT MIN(commitment_tree_position) FROM sapling_received_notes WHERE account = :account_id AND spent IS NULL)) AS "Block with first unspent note"
"#;
