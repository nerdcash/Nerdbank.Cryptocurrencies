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
	/// <param name="outgoing">A collection of individual spend details with amounts and recipients belonging to this transaction, excluding those identified as <paramref name="change"/>.</param>
	/// <param name="incoming">Notes received in this transaction, excluding those identified as <paramref name="change"/>.</param>
	/// <param name="change">Notes that are both sent and received in this transaction such that it appears to be implicit change.</param>
	public Transaction(
		TxId transactionId,
		uint? minedHeight,
		bool expiredUnmined,
		DateTimeOffset? when,
		decimal netChange,
		decimal? fee,
		ImmutableArray<LineItem> outgoing,
		ImmutableArray<LineItem> incoming,
		ImmutableArray<LineItem> change)
	{
		this.TransactionId = transactionId.PrecacheString();
		this.MinedHeight = minedHeight;
		this.ExpiredUnmined = expiredUnmined;
		this.When = when;
		this.NetChange = netChange;
		this.Fee = fee;
		this.Outgoing = outgoing;
		this.Incoming = incoming;
		this.Change = change;
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
	public DateTimeOffset? When { get; }

	/// <summary>
	/// Gets the net balance change applied by this transaction.
	/// </summary>
	public decimal NetChange { get; }

	/// <summary>
	/// Gets the transaction fee, as a <em>positive</em> value.
	/// </summary>
	/// <remarks>
	/// This fee is only relevant to the account's balance when <see cref="IsIncoming"/> is <see langword="false" />.
	/// </remarks>
	public decimal? Fee { get; }

	/// <summary>
	/// Gets the individual sent notes in this transaction, excluding those identified as <see cref="Change"/>.
	/// </summary>
	public ImmutableArray<LineItem> Outgoing { get; }

	/// <summary>
	/// Gets the individual received notes in this transaction, excluding those identified as <see cref="Change"/>.
	/// </summary>
	public ImmutableArray<LineItem> Incoming { get; }

	/// <summary>
	/// Gets the notes that are both sent and received in this transaction such that they appear to be implicit change.
	/// </summary>
	public ImmutableArray<LineItem> Change { get; }

	/// <summary>
	/// Gets a value indicating whether this transaction did not originate from this account.
	/// </summary>
	public bool IsIncoming => this.Outgoing.IsEmpty;
}
