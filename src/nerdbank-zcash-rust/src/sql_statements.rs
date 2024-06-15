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
		txo.output_pool,
		coalesce(
			txo.from_account_id,
			(SELECT account_id
			 FROM sapling_received_notes srn
			 WHERE srn.id = (
			 	SELECT sapling_received_note_id
			 	FROM sapling_received_note_spends srns
			 	WHERE transaction_id = tx.id_tx
			 )),
			(SELECT account_id 
			 FROM orchard_received_notes srn 
			 WHERE srn.id = (
			 	SELECT orchard_received_note_id
			 	FROM orchard_received_note_spends srns
			 	WHERE transaction_id = tx.id_tx
			 ))
		) AS from_account_id,
		txo.to_account_id,
		txo.to_address,
		coalesce(s.diversifier, o.diversifier) AS diversifier,
		txo.value,
		txo.memo
	FROM v_transactions t
	LEFT OUTER JOIN v_tx_outputs txo ON t.txid = txo.txid
	LEFT OUTER JOIN transactions tx ON tx.txid = t.txid
	LEFT OUTER JOIN sapling_received_notes s ON txo.output_pool = 2 AND s.tx = tx.id_tx AND s.output_index = txo.output_index
	LEFT OUTER JOIN orchard_received_notes o ON txo.output_pool = 3 AND o.tx = tx.id_tx AND o.action_index = txo.output_index
	WHERE (:account_id IS NULL OR t.account_id = :account_id) 
		AND (from_account_id IS NOT NULL OR to_account_id IS NOT NULL) -- ignore transactions that probably aren't fully initialized
		AND (t.mined_height IS NULL OR :starting_block IS NULL OR t.mined_height >= :starting_block)
		AND (t.mined_height IS NULL OR :ending_block IS NULL OR t.mined_height <= :ending_block)
	ORDER BY t.mined_height, t.tx_index
"#;

// TODO: update this to consider UTXOs in "Block with first unspent note" column.
// Note that WalletDb::get_min_unspent_height provides the rebirth height at the wallet level (instead of the account level).
pub(crate) const GET_BIRTHDAY_HEIGHTS: &str = r#"
	SELECT
		(SELECT birthday_height FROM accounts WHERE id = :account_id) AS "Original birthday height",
		(SELECT MIN(mined_height) FROM v_transactions WHERE account_id = :account_id) AS "Block with first note",
		(SELECT MIN(t.block)
			FROM transactions t 
			LEFT OUTER JOIN sapling_received_notes s ON s.tx = t.id_tx
			LEFT OUTER JOIN sapling_received_note_spends ss ON ss.sapling_received_note_id = s.id
			LEFT OUTER JOIN orchard_received_notes o ON o.tx = t.id_tx
			LEFT OUTER JOIN orchard_received_note_spends os ON os.orchard_received_note_id = o.id
			WHERE (s.account_id = :account_id AND ss.transaction_id IS NULL) OR (o.account_id = :account_id AND os.transaction_id IS NULL)
		) AS "Block with first unspent note"
"#;

// The v_tx_outputs view doesn't include transparent UTXOs, so we filter them out (for good measure) and add them via UNION with the utxos table.
pub(crate) const GET_UNSPENT_NOTES: &str = r#"
	SELECT
		tx.block,
		txo.value,
		txo.output_pool,
		COALESCE(txo.from_account_id = txo.to_account_id, 0) AS is_change
	FROM v_tx_outputs txo
	INNER JOIN transactions tx ON tx.txid = txo.txid
	LEFT OUTER JOIN sapling_received_notes s ON txo.output_pool = 2 AND s.tx = tx.id_tx AND s.output_index = txo.output_index
	LEFT OUTER JOIN sapling_received_note_spends ss ON ss.sapling_received_note_id = s.id
	LEFT OUTER JOIN orchard_received_notes o ON txo.output_pool = 3 AND o.tx = tx.id_tx AND o.action_index = txo.output_index
	LEFT OUTER JOIN orchard_received_note_spends os ON os.orchard_received_note_id = o.id
	WHERE txo.to_account_id = :account_id AND ss.transaction_id IS NULL AND os.transaction_id IS NULL AND txo.output_pool > 0

	UNION
	
	SELECT
		height,
		value_zat,
		0, -- output_pool
		0  -- is_change
	FROM utxos
	LEFT OUTER JOIN transparent_received_output_spends j ON utxos.id = j.transparent_received_output_id
	WHERE received_by_account_id = :account_id AND j.transaction_id IS NULL
"#;

pub(crate) const GET_UNSPENT_TRANSPARENT_NOTES: &str = r#"
	SELECT
		height,
		value_zat,
		address
	FROM utxos
	LEFT OUTER JOIN transparent_received_output_spends j ON utxos.id = j.transparent_received_output_id
	WHERE received_by_account_id = :account_id AND j.transaction_id IS NULL
	ORDER BY height
"#;
