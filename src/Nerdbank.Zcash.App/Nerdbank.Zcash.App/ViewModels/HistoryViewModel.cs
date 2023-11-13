// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using DynamicData;
using Nerdbank.Cryptocurrencies.Exchanges;

namespace Nerdbank.Zcash.App.ViewModels;

public class HistoryViewModel : ViewModelBaseWithAccountSelector, IHasTitle
{
	private ObservableAsPropertyHelper<bool> exchangeRatePerTransactionHasBeenDismissed;

	private TransactionViewModel? selectedTransaction;

	[Obsolete("For design-time use only", error: true)]
	public HistoryViewModel()
		: this(new DesignTimeViewModelServices())
	{
		this.Transactions.AddRange(new TransactionViewModel[]
		{
			new() { Amount = ZEC(1.2345m), RunningBalance = ZEC(1.2345m), IsIncoming = true, OtherPartyName = "Andrew Arnott", Memo = "For the pizza", TransactionId = "12345abc", When = DateTimeOffset.Now - TimeSpan.FromDays(200) },
			new() { Amount = ZEC(-0.5m), RunningBalance = ZEC(1.2345m - 0.5m), IsIncoming = false, OtherPartyName = "Red Rock Cafe", Memo = "Hot Chocolate", TransactionId = "1e62b7", When = DateTimeOffset.Now - TimeSpan.FromDays(35) },
			new() { Amount = ZEC(2m), RunningBalance = ZEC(1.2345m - 0.5m + 2m), IsIncoming = true, OtherPartyName = "Employer", Memo = "Paycheck", TransactionId = "236ba", When = DateTimeOffset.Now - TimeSpan.FromDays(2) },
		});

		SecurityAmount ZEC(decimal amount) => this.SelectedSecurity.Amount(amount);
	}

	public HistoryViewModel(IViewModelServices viewModelServices)
		: base(viewModelServices)
	{
		this.HideExchangeRateExplanationCommand = ReactiveCommand.Create(() => { this.ViewModelServices.Settings.ExchangeRatePerTransactionHasBeenDismissed = true; });

		this.exchangeRatePerTransactionHasBeenDismissed = viewModelServices.Settings.WhenAnyValue(s => s.ExchangeRatePerTransactionHasBeenDismissed, d => !d)
			.ToProperty(this, nameof(this.ExchangeRateExplanationIsVisible));

		this.LinkProperty(nameof(this.SelectedSecurity), nameof(this.AmountColumnHeader));
		this.LinkProperty(nameof(this.SelectedTransaction), nameof(this.IsTransactionDetailsVisible));
	}

	public string Title => "History";

	public SyncProgressData SyncProgress { get; } = new SyncProgressData();

	public ObservableCollection<TransactionViewModel> Transactions { get; } = new();

	public string WhenColumnHeader => "When";

	public string AmountColumnHeader => this.SelectedSecurity.TickerSymbol;

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

	public string ExchangeRateExplanation => "The value in fiat currency is based on the exchange rate at the time of each transaction.";

	public bool ExchangeRateExplanationIsVisible => this.exchangeRatePerTransactionHasBeenDismissed.Value;

	public ReactiveCommand<Unit, Unit> HideExchangeRateExplanationCommand { get; }

	public string HideExchangeRateExplanationCommandCaption => "Got it";
}
