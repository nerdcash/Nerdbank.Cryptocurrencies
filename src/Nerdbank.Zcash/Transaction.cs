// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

namespace Nerdbank.Zcash;

/// <summary>
/// Describes a Zcash transaction.
/// </summary>
/// <param name="TransactionId">The transaction ID.</param>
/// <param name="BlockNumber">The block that mined this transaction.</param>
/// <param name="When">The timestamp on this transaction.</param>
/// <param name="IsUnconfirmed">A value indicating whether this transaction is stil waiting in the mempool, unmined.</param>
/// <param name="Spent">The amount of ZEC that was spent in this transaction.</param>
/// <param name="Received">The amount of ZEC that was received in this transaction.</param>
/// <param name="Sends">A collection of individual spend details with amounts and recipients belonging to this transaction.</param>
/// <param name="Notes">Notes received in this transaction.</param>
/// <param name="IsIncoming"><see langword="true"/> if the transaction was sent by a different wallet; <see langword="false" /> otherwise.</param>
public partial record Transaction(
	string TransactionId,
	uint BlockNumber,
	DateTime When,
	bool IsUnconfirmed,
	decimal Spent,
	decimal Received,
	ImmutableArray<Transaction.SendItem> Sends,
	ImmutableArray<Transaction.RecvItem> Notes,
	bool IsIncoming)
{
	/// <summary>
	/// Gets the net balance change applied by this transaction.
	/// </summary>
	public decimal NetChange => this.Received - this.Spent;

	/// <summary>
	/// Gets the transaction fee.
	/// </summary>
	public decimal Fee
	{
		get
		{
			if (this.IsIncoming)
			{
				// https://github.com/zingolabs/zingolib/issues/553
				throw new NotSupportedException("ZingoLib doesn't expose the transaction details necessary to calculate the fee for incoming transactions.");
			}

			return this.Spent - this.Received - this.Sends.Sum(s => s.Amount);
		}
	}
}
