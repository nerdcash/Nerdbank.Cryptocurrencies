// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using DynamicData;
using Nerdbank.Cryptocurrencies.Exchanges;

namespace Nerdbank.Zcash.App.ViewModels;

public class HistoryViewModel : ViewModelBase
{
	private readonly IViewModelServicesWithSelectedAccount viewModelServices;
	private TransactionViewModel? selectedTransaction;

	[Obsolete("For design-time use only", error: true)]
	public HistoryViewModel()
		: this(new DesignTimeViewModelServices())
	{
		this.Transactions.AddRange(new TransactionViewModel[]
		{
			new() { Amount = ZEC(1.2345m), RunningBalance = ZEC(1.2345m), IsIncoming = true, OtherPartyName = "Andrew Arnott", Memo = "For the pizza", TransactionId = "12345abc", When = DateTimeOffset.Now.Subtract(TimeSpan.FromDays(1)) },
			new() { Amount = ZEC(-0.5m), RunningBalance = ZEC(1.2345m - 0.5m), IsIncoming = false, OtherPartyName = "Red Rock Cafe", Memo = "Hot Chocolate", TransactionId = "1e62b7", When = DateTimeOffset.Now },
		});

		SecurityAmount ZEC(decimal amount) => new(amount, this.viewModelServices.SelectedAccount.Network.AsSecurity());
	}

	public HistoryViewModel(IViewModelServicesWithSelectedAccount viewModelServices)
	{
		this.viewModelServices = viewModelServices;

		this.LinkProperty(nameof(this.SelectedTransaction), nameof(this.IsTransactionDetailsVisible));
	}

	public string Title => "History";

	public ObservableCollection<TransactionViewModel> Transactions { get; } = new();

	public string WhenColumnHeader => "When";

	public string AmountColumnHeader => this.viewModelServices.SelectedAccount.Network.GetTickerName();

	public string FiatAmountColumnHeader => "USD";

	public string OtherPartyNameColumnHeader => "Name";

	public string MemoColumnHeader => "Memo";

	public string RunningBalanceColumnHeader => "Balance";

	public TransactionViewModel? SelectedTransaction
	{
		get => this.selectedTransaction;
		set => this.RaiseAndSetIfChanged(ref this.selectedTransaction, value);
	}

	public bool IsTransactionDetailsVisible => this.SelectedTransaction is not null;
}
