// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Diagnostics;
using MessagePack;

namespace Nerdbank.Zcash.App.Models;

[DebuggerDisplay($"{{{nameof(DebuggerDisplay)},nq}}")]
[MessagePackObject]
public class ZcashTransaction : ReactiveObject, IPersistableData
{
	private uint? blockNumber;
	private DateTimeOffset? when;
	private bool isDirty;
	private string mutableMemo = string.Empty;
	private string otherPartyName = string.Empty;
	private decimal? fee;

	public ZcashTransaction()
	{
	}

	[Key(0)]
	public required string TransactionId { get; init; }

	[Key(1)]
	public uint? BlockNumber
	{
		get => this.blockNumber;
		set => this.RaiseAndSetIfChanged(ref this.blockNumber, value);
	}

	[Key(2)]
	public required bool IsIncoming { get; init; }

	[Key(3)]
	public DateTimeOffset? When
	{
		get => this.when;
		set => this.RaiseAndSetIfChanged(ref this.when, value);
	}

	[IgnoreMember]
	public decimal NetChange => this.RecvItems.Sum(i => i.Amount) - this.SendItems.Sum(i => i.Amount) + (this.Fee ?? 0);

	/// <summary>
	/// Gets the fee paid for this transaction, if known.
	/// </summary>
	/// <value>When specified, this is represented as a negative value.</value>
	[Key(4)]
	public decimal? Fee
	{
		get => this.fee;
		init
		{
			Requires.Range(value is not > 0, nameof(value), "Non-positive values only.");
			this.fee = value;
		}
	}

	[Key(5)]
	public string MutableMemo
	{
		get => this.mutableMemo;
		set => this.RaiseAndSetIfChanged(ref this.mutableMemo, value);
	}

	[Key(6)]
	public string OtherPartyName
	{
		get => this.otherPartyName;
		set => this.RaiseAndSetIfChanged(ref this.otherPartyName, value);
	}

	[Key(7), MaybeNull]
	public ZcashAddress OtherPartyAddress { get; init; }

	[Key(8)]
	public ImmutableArray<Transaction.SendItem> SendItems { get; init; }

	[Key(9)]
	public ImmutableArray<Transaction.RecvItem> RecvItems { get; init; }

	[IgnoreMember]
	public bool IsDirty
	{
		get => this.isDirty;
		set => this.RaiseAndSetIfChanged(ref this.isDirty, value);
	}

	private string DebuggerDisplay => $"{this.TransactionId} ({this.NetChange})";

	/// <summary>
	/// Gets the incoming amount in this transaction that was received with one of the receivers in a particular address.
	/// </summary>
	/// <param name="address">The address of interest.</param>
	/// <returns>The sum of the amounts in the notes destined for the given address.</returns>
	public decimal GetAmountReceivedUsingAddress(ZcashAddress address)
	{
		IEnumerable<decimal> matchingNoteAmounts =
			from item in this.RecvItems
			where item.ToAddress.IsMatch(address).HasFlag(ZcashAddress.Match.MatchingReceiversFound)
			select item.Amount;
		return matchingNoteAmounts.Sum();
	}
}
