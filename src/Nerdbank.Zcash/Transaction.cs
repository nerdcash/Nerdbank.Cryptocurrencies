// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

namespace Nerdbank.Zcash;

/// <summary>
/// Describes a Zcash transaction.
/// </summary>
public partial record Transaction
{
	private readonly Bytes32 txid;

	private string? txidString;

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
		ReadOnlySpan<byte> transactionId,
		uint? minedHeight,
		bool expiredUnmined,
		DateTime when,
		decimal netChange,
		decimal fee,
		ImmutableArray<SendItem> outgoing,
		ImmutableArray<RecvItem> incoming)
	{
		this.txid = new(transactionId);
		this.MinedHeight = minedHeight;
		this.ExpiredUnmined = expiredUnmined;
		this.When = when;
		this.NetChange = netChange;
		this.Fee = fee;
		this.Outgoing = outgoing;
		this.Incoming = incoming;
	}

	/// <summary>
	/// Gets the raw bytes of the transaction id.
	/// </summary>
	public ReadOnlySpan<byte> TxId => this.txid[..];

	/// <summary>
	/// Gets the transaction ID as a hex string (with byte order reversed, as is convention).
	/// </summary>
	public string TransactionId
	{
		get
		{
			if (this.txidString is null)
			{
				// txid's are traditionally rendered as hex, in the opposite order of the bytes in the txid.
				Span<byte> txidReversed = stackalloc byte[32];
				this.txid[..].CopyTo(txidReversed);
				txidReversed.Reverse();
				this.txidString = Convert.ToHexString(txidReversed);
			}

			return this.txidString;
		}
	}

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
	public DateTime When { get; }

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
}
