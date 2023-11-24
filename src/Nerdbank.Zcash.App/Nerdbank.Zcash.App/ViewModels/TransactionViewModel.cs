// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Cryptocurrencies;

namespace Nerdbank.Zcash.App.ViewModels;

public class TransactionViewModel : ViewModelBase, IViewModel<ZcashTransaction>
{
	private readonly IViewModelServices viewModelServices;
	private SecurityAmount runningBalance;

	public TransactionViewModel(ZcashTransaction transaction, IViewModelServices viewModelServices)
	{
		this.Model = transaction;
		this.viewModelServices = viewModelServices;

		if (transaction.SendItems.Length == 1)
		{
			this.Memo = transaction.SendItems[0].Memo.Message;
		}
		else if (transaction.RecvItems.Length == 1)
		{
			this.Memo = transaction.RecvItems[0].Memo.Message;
		}

		this.LinkProperty(transaction, nameof(transaction.BlockNumber), nameof(this.BlockNumber));
		this.LinkProperty(transaction, nameof(transaction.When), nameof(this.When));
		this.LinkProperty(transaction, nameof(transaction.MutableMemo), nameof(this.MutableMemo));
		this.LinkProperty(transaction, nameof(transaction.OtherPartyName), nameof(this.OtherPartyName));

		this.LinkProperty(nameof(this.When), nameof(this.WhenColumnFormatted));
		this.LinkProperty(nameof(this.When), nameof(this.WhenDetailedFormatted));
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

	public SecurityAmount Amount => this.Model.Amount;

	public string AmountCaption => "Amount";

	public SecurityAmount? Fee => this.Model.Fee is decimal fee ? this.Model.Security.Amount(fee) : null;

	public string FeeCaption => "Fee";

	public SecurityAmount? AlternateAmount => this.Amount * this.Model.ExchangeRate;

	public ZcashAddress? OtherPartyAddress => this.Model.OtherPartyAddress;

	public string OtherPartyName
	{
		get => this.Model.OtherPartyName;
		set => this.Model.OtherPartyName = value;
	}

	public string OtherPartyNameCaption => "Name";

	public string? Memo { get; }

	public string MemoCaption => "Shared Memo";

	public string MutableMemoCaption => "Private Memo";

	public string MutableMemo
	{
		get => this.Model.MutableMemo;
		set => this.Model.MutableMemo = value;
	}

	public bool IsIncoming => this.Model.IsIncoming;

	public SecurityAmount RunningBalance
	{
		get => this.runningBalance;
		internal set => this.RaiseAndSetIfChanged(ref this.runningBalance, value);
	}

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
