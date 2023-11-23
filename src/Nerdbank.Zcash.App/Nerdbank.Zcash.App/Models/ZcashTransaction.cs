// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using MessagePack;
using Nerdbank.Cryptocurrencies.Exchanges;

namespace Nerdbank.Zcash.App.Models;

[DebuggerDisplay($"{{{nameof(DebuggerDisplay)}}},nq")]
[MessagePackObject]
public class ZcashTransaction : ReactiveObject, IPersistableData
{
	private uint? blockNumber;
	private DateTimeOffset? when;
	private bool isDirty;
	private string mutableMemo = string.Empty;
	private ExchangeRate? exchangeRate;
	private string otherPartyName = string.Empty;

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

	[Key(4)]
	public required SecurityAmount Amount { get; init; }

	[Key(5)]
	public decimal? Fee { get; init; }

	[Key(6)]
	public string? Memo { get; init; }

	[Key(7)]
	public string MutableMemo
	{
		get => this.mutableMemo;
		set => this.RaiseAndSetIfChanged(ref this.mutableMemo, value);
	}

	[Key(8)]
	public string OtherPartyName
	{
		get => this.otherPartyName;
		set => this.RaiseAndSetIfChanged(ref this.otherPartyName, value);
	}

	[Key(9), MaybeNull]
	public ZcashAddress OtherPartyAddress { get; init; }

	[IgnoreMember]
	public ExchangeRate? ExchangeRate
	{
		get => this.exchangeRate;
		set => this.RaiseAndSetIfChanged(ref this.exchangeRate, value);
	}

	[IgnoreMember]
	public bool IsDirty
	{
		get => this.isDirty;
		set => this.RaiseAndSetIfChanged(ref this.isDirty, value);
	}

	private string DebuggerDisplay => $"{this.TransactionId} ({this.Amount})";

	/// <summary>
	/// Gets the incoming amount in this transaction that was received with one of the receivers in a particular address.
	/// </summary>
	/// <param name="address">The address of interest.</param>
	/// <returns>The sum of the amounts in the notes destined for the given address.</returns>
	public SecurityAmount GetAmountReceivedUsingAddress(ZcashAddress address)
	{
		// TODO: implement this filter when we actually store memos.
		return this.Amount;
	}
}
