// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using Nerdbank.Cryptocurrencies.Exchanges;

namespace Nerdbank.Zcash.App.ViewModels;

public class TransactionViewModel : ViewModelBase
{
	private uint? blockNumber = 2_200_235;
	private DateTimeOffset? when = DateTimeOffset.Now;
	private string otherPartyName = string.Empty;
	private SecurityAmount runningBalance;
	private string mutableMemo = string.Empty;

	public TransactionViewModel()
	{
		this.LinkProperty(nameof(this.When), nameof(this.WhenColumnFormatted));
		this.LinkProperty(nameof(this.When), nameof(this.WhenDetailedFormatted));
	}

	public uint? BlockNumber
	{
		get => this.blockNumber;
		set => this.RaiseAndSetIfChanged(ref this.blockNumber, value);
	}

	public string BlockNumberFormatted => $"{this.BlockNumber:N0}";

	public string BlockNumberCaption => "Block #";

	public required string TransactionId { get; init; }

	public string TransactionIdCaption => "Transaction ID";

	public DateTimeOffset? When
	{
		get => this.when;
		set => this.RaiseAndSetIfChanged(ref this.when, value);
	}

	public string WhenColumnFormatted
	{
		get
		{
			if (this.When is null)
			{
				return string.Empty;
			}

			int daysAgo = (DateTimeOffset.UtcNow - this.When.Value).Days;
			if (daysAgo < 7)
			{
				return $"{this.When:ddd}";
			}

			if (daysAgo < 6 * 30 || this.When.Value.Year == DateTimeOffset.UtcNow.Year)
			{
				return $"{this.When:d MMM}";
			}

			return $"{this.When:d}";
		}
	}

	public string WhenDetailedFormatted => this.When?.ToString("g") ?? string.Empty;

	public string WhenCaption => "When";

	public required SecurityAmount Amount { get; init; }

	public decimal FiatAmount => this.Amount.Amount * 30;

	public string FiatAmountFormatted
	{
		get
		{
			NumberFormatInfo customFormat = (NumberFormatInfo)CultureInfo.CurrentCulture.NumberFormat.Clone();
			customFormat.CurrencyNegativePattern = 1; // Change negative sign pattern to negative sign (-10)
			return this.FiatAmount.ToString("C", customFormat);
		}
	}

	public string AmountCaption => "Amount";

	public ZcashAddress? OtherPartyAddress { get; init; } = ZcashAddress.Decode("u1wwsl42efxdj727vfcgmcf7wgxdqjjen4wqu79666ujf4qj4sqgezjemnaf23dlsgct3etneqrf2py2qws0lt2jfxv0n9cx5yr7l9vwa4hqvcznu0kxz90vpz4tgrd327wl4s875883w0rq6zjkp67c6qthdwwa6kcw8pv3699sfy27qa");

	public string OtherPartyName
	{
		get => this.otherPartyName;
		set => this.RaiseAndSetIfChanged(ref this.otherPartyName, value);
	}

	public string OtherPartyNameCaption => "Name";

	public string? Memo { get; init; }

	public string MemoCaption => "Shared Memo";

	public string MutableMemoCaption => "Private Memo";

	public string MutableMemo
	{
		get => this.mutableMemo;
		set => this.RaiseAndSetIfChanged(ref this.mutableMemo, value);
	}

	public required bool IsIncoming { get; init; }

	public SecurityAmount RunningBalance
	{
		get => this.runningBalance;
		set => this.RaiseAndSetIfChanged(ref this.runningBalance, value);
	}
}
