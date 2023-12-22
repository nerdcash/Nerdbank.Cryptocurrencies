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
