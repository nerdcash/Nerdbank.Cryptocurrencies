// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using DynamicData;
using Nerdbank.Cryptocurrencies;
using Nerdbank.Cryptocurrencies.Exchanges;

namespace Nerdbank.Zcash.App.ViewModels;

public class TransactionViewModel : ViewModelBase, IViewModel<ZcashTransaction>
{
	private readonly TradingPair tradingPair;
	private readonly IViewModelServices viewModelServices;
	private readonly ObservableCollection<LineItem> lineItems = new();
	private SecurityAmount runningBalance;
	private SecurityAmount? alternateAmount;

	public TransactionViewModel(TradingPair tradingPair, ZcashTransaction transaction, IViewModelServices viewModelServices)
	{
		this.tradingPair = tradingPair;
		this.Model = transaction;
		this.viewModelServices = viewModelServices;
		this.LineItems = new ReadOnlyObservableCollection<LineItem>(this.lineItems);

		if (transaction.SendItems.Length == 1)
		{
			this.Memo = transaction.SendItems[0].Memo.Message;
		}
		else if (transaction.RecvItems.Length == 1)
		{
			this.Memo = transaction.RecvItems[0].Memo.Message;
		}

		this.InitializeMemos();

		this.LinkProperty(transaction, nameof(transaction.BlockNumber), nameof(this.BlockNumber));
		this.LinkProperty(transaction, nameof(transaction.When), nameof(this.When));
		this.LinkProperty(transaction, nameof(transaction.MutableMemo), nameof(this.MutableMemo));
		this.LinkProperty(transaction, nameof(transaction.OtherPartyName), nameof(this.OtherPartyName));

		this.LinkProperty(nameof(this.When), nameof(this.WhenColumnFormatted));
		this.LinkProperty(nameof(this.When), nameof(this.WhenDetailedFormatted));

		this.LinkProperty(nameof(this.AlternateAmount), nameof(this.AlternateAmountUserEdit));

		if (transaction.When is not null && viewModelServices.ExchangeData.TryGetExchangeRate(transaction.When.Value, tradingPair, out ExchangeRate exchangeRate))
		{
			this.AlternateAmount = this.Amount * exchangeRate;
		}
	}

	public ZcashTransaction Model { get; }

	public uint? BlockNumber => this.Model.BlockNumber;

	public string BlockNumberFormatted => $"{this.BlockNumber:N0}";

	public string BlockNumberCaption => "Block #";

	public string TransactionId => this.Model.TransactionId;

	public string TransactionIdCaption => "Transaction ID";

	public DateTimeOffset? When => this.Model.When?.ToLocalTime();

	public string WhenColumnFormatted
	{
		get
		{
			if (this.When is null)
			{
				return string.Empty;
			}

			// If this happened today, just display the time.
			if (this.When.Value.Date == DateTimeOffset.Now.Date)
			{
				return $"{this.When:h:mm tt}";
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

	public SecurityAmount Amount => this.Security.Amount(this.Model.NetChange);

	public string AmountCaption => "Amount";

	public SecurityAmount? Fee => this.Model.Fee is decimal fee ? this.Security.Amount(fee) : null;

	public string FeeCaption => "Fee";

	public SecurityAmount? AlternateAmount
	{
		get => this.alternateAmount;
		internal set => this.RaiseAndSetIfChanged(ref this.alternateAmount, value);
	}

	public decimal? AlternateAmountUserEdit
	{
		get => this.AlternateAmount?.Amount;
		set
		{
			this.AlternateAmount = value is null ? null : this.Security.Amount(value.Value);

			// Record the exchange rate.
			if (value is not null && this.When is not null)
			{
				ExchangeRate rate = new(this.AlternateSecurity.Amount(value.Value), this.Amount);
				this.viewModelServices.ExchangeData.SetExchangeRate(this.When.Value, rate);
			}

			this.RaisePropertyChanged();
		}
	}

	public ZcashAddress? OtherPartyAddress => this.Model.OtherPartyAddress;

	public string OtherPartyName
	{
		get => this.Model.OtherPartyName;
		set => this.Model.OtherPartyName = value;
	}

	public string OtherPartyNameCaption => "Name";

	public string? Memo { get; }

	public bool IsSingleLineItem => this.LineItems.Count == 1;

	public string MemoCaption => "Shared Memo";

	public string MutableMemoCaption => "Private Memo";

	public string MutableMemo
	{
		get => this.Model.MutableMemo;
		set => this.Model.MutableMemo = value;
	}

	public string LineItemsCaption => "Line Items";

	public ReadOnlyObservableCollection<LineItem> LineItems { get; }

	public string AmountColumnHeader => "Amount";

	public bool IsIncoming => this.Model.IsIncoming;

	public SecurityAmount RunningBalance
	{
		get => this.runningBalance;
		internal set => this.RaiseAndSetIfChanged(ref this.runningBalance, value);
	}

	private Security Security => this.tradingPair.TradeInterest;

	private Security AlternateSecurity => this.tradingPair.Basis;

	private void InitializeMemos()
	{
		if (this.Model.SendItems.Length > 0)
		{
			this.lineItems.AddRange(this.Model.SendItems.Select(i => new LineItem(this.Security.Amount(-i.Amount), i.Memo.Message)));
		}
		else if (this.Model.RecvItems.Length > 0)
		{
			if (this.Model.RecvItems.Length == 2)
			{
				// Look for a common pattern of splitting a payment across two pools. If that's what this is, just report the two line items as one.
				Transaction.RecvItem first = this.Model.RecvItems[0];
				Transaction.RecvItem second = this.Model.RecvItems[1];
				if (first.Memo.Equals(second.Memo) && first.Pool != second.Pool)
				{
					this.lineItems.Add(new LineItem(this.Security.Amount(first.Amount + second.Amount), first.Memo.Message));
					return;
				}
			}

			this.lineItems.AddRange(this.Model.RecvItems.Select(i => new LineItem(this.Security.Amount(i.Amount), i.Memo.Message)));
		}
	}

	public record LineItem(SecurityAmount Amount, string? Memo);

	internal class DateComparer : IComparer<TransactionViewModel>, IOptimizedComparer<TransactionViewModel>
	{
		private DateComparer()
		{
		}

		public static DateComparer Instance { get; } = new();

		public int Compare(TransactionViewModel? x, TransactionViewModel? y)
		{
			if (x is null)
			{
				return y is null ? 0 : -1;
			}

			if (y is null)
			{
				return 1;
			}

			return (x.When ?? DateTimeOffset.MaxValue).CompareTo(y.When ?? DateTimeOffset.MaxValue);
		}

		public bool IsPropertySignificant(string propertyName) => propertyName == nameof(TransactionViewModel.When);
	}
}
