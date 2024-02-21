// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

namespace Nerdbank.Zcash;

/// <summary>
/// Describes a Zcash transaction.
/// </summary>
public partial record Transaction
{
	/// <summary>
	/// Initializes a new instance of the <see cref="Transaction"/> class.
	/// </summary>
	/// <param name="transactionId">The transaction ID.</param>
	/// <param name="minedHeight">The block that mined this transaction.</param>
	/// <param name="expiredUnmined">A value indicating whether the transaction expired before it was mined.</param>
	/// <param name="when">The timestamp on this transaction.</param>
	/// <param name="netChange">The net balance change applied by this transaction.</param>
	/// <param name="fee">The transaction fee.</param>
	/// <param name="outgoing">A collection of individual spend details with amounts and recipients belonging to this transaction.</param>
	/// <param name="incoming">Notes received in this transaction.</param>
	public Transaction(
		TxId transactionId,
		uint? minedHeight,
		bool expiredUnmined,
		DateTime? when,
		decimal netChange,
		decimal fee,
		ImmutableArray<SendItem> outgoing,
		ImmutableArray<RecvItem> incoming)
	{
		this.TransactionId = transactionId.PrecacheString();
		this.MinedHeight = minedHeight;
		this.ExpiredUnmined = expiredUnmined;
		this.When = when;
		this.NetChange = netChange;
		this.Fee = fee;
		this.Outgoing = outgoing;
		this.Incoming = incoming;
	}

	/// <summary>
	/// Gets the transaction ID.
	/// </summary>
	public TxId TransactionId { get; }

	/// <summary>
	/// Gets the block number this transaction was mined in, if applicable.
	/// </summary>
	public uint? MinedHeight { get; }

	/// <summary>
	/// Gets a value indicating whether this transaction expired before it was mined.
	/// </summary>
	public bool ExpiredUnmined { get; }

	/// <summary>
	/// Gets the timestamp on the block this transaction was mined in.
	/// </summary>
	public DateTime? When { get; }

	/// <summary>
	/// Gets the net balance change applied by this transaction.
	/// </summary>
	public decimal NetChange { get; }

	/// <summary>
	/// Gets the transaction fee.
	/// </summary>
	public decimal Fee { get; }

	/// <summary>
	/// Gets the individual sent notes in this transaction.
	/// </summary>
	public ImmutableArray<SendItem> Outgoing { get; }

	/// <summary>
	/// Gets the individual received notes in this transaction.
	/// </summary>
	public ImmutableArray<RecvItem> Incoming { get; }

	/// <summary>
	/// Gets a value indicating whether this transaction sends funds from this account.
	/// </summary>
	/// <remarks>
	/// Note this only indicates that funds were sent from this account,
	/// not that they were sent to <em>another</em> account.
	/// They may in fact have been sent to the same account.
	/// But this value is useful to determine whether the <see cref="Fee"/> came out of this account.
	/// </remarks>
	public bool IsOutgoing => this.Outgoing.Length > 0;
}
