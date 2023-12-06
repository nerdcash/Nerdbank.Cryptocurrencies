// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Collections.ObjectModel;
using DynamicData;
using Microsoft.VisualStudio.Threading;
using Nerdbank.Cryptocurrencies;
using Nerdbank.Cryptocurrencies.Exchanges;

namespace Nerdbank.Zcash.App.ViewModels;

public class HistoryViewModel : ViewModelBaseWithAccountSelector, IHasTitle
{
	private Security alternateSecurity;
	private IDisposable? transactionsSubscription;
	private ObservableAsPropertyHelper<bool> exchangeRateExplanationIsVisible;
	private ObservableAsPropertyHelper<bool> isAlternateAmountColumnVisible;
	private TransactionViewModel? selectedTransaction;

	[Obsolete("For design-time use only", error: true)]
	public HistoryViewModel()
		: this(new DesignTimeViewModelServices())
	{
		this.Transactions.AddRange(new TransactionViewModel[]
		{
			MockTx(-0.5m, "Hot Chocolate", TimeSpan.FromDays(35), "1e62b7", "Red Rock Cafe"),
			MockTx(1.2345m, "For the pizza", TimeSpan.FromDays(200), "12345abc", "Andrew Arnott"),
			MockTx(2m, "Paycheck", TimeSpan.FromDays(2), "236ba", "Employer"),
		});

		TransactionViewModel MockTx(decimal amount, string memo, TimeSpan age, string txid, string otherPartyName)
		{
			ImmutableArray<ZcashTransaction.LineItem> sends = amount < 0
				? [new ZcashTransaction.LineItem { Amount = -amount, Memo = Memo.FromMessage(memo), ToAddress = ZcashAddress.Decode("t1N7bGKWqoWVrv4XGSzrfUsoKkCxNFAutQZ") }]
				: [];
			ImmutableArray<ZcashTransaction.LineItem> receives = amount > 0
				? [new ZcashTransaction.LineItem { Amount = amount, Memo = Memo.FromMessage(memo), ToAddress = ZcashAddress.Decode("t1XkdzRXnCguCQtrigDUtgyz5SVtLYaAXBi") }]
				: [];
			return new TransactionViewModel(
				new TradingPair(Security.USD, this.SelectedSecurity),
				new ZcashTransaction
				{
					IsIncoming = amount > 0,
					Fee = amount > 0 ? null : -0.0001m,
					TransactionId = txid,
					When = DateTimeOffset.UtcNow - age,
					SendItems = sends,
					RecvItems = receives,
				},
				this)
			{
				OtherPartyName = otherPartyName,
			};
		}
	}

	public HistoryViewModel(IViewModelServices viewModelServices)
		: base(viewModelServices)
	{
		// Snapshot the alternate currency so that we don't have to worry about it changing out from under us.
		// Users who change it will have to reinitialize this view to get the new alternate currency values.
		this.alternateSecurity = viewModelServices.Settings.AlternateCurrency;

		this.HideExchangeRateExplanationCommand = ReactiveCommand.Create(() => { this.ViewModelServices.Settings.ExchangeRatePerTransactionHasBeenDismissed = true; });

		this.SyncProgress = new SyncProgressData(this);

		this.exchangeRateExplanationIsVisible = this.WhenAnyValue(
			vm => vm.ViewModelServices.Settings.ExchangeRatePerTransactionHasBeenDismissed,
			vm => vm.SelectedSecurity.IsTestNet,
			vm => vm.Transactions.Count,
			(dismissed, isTestNet, txCount) => !dismissed && !isTestNet && txCount > 0)
			.ToProperty(this, nameof(this.ExchangeRateExplanationIsVisible));

		this.isAlternateAmountColumnVisible = this.WhenAnyValue(
			vm => vm.SelectedSecurity,
			s => !s.IsTestNet).ToProperty(this, nameof(this.IsAlternateAmountColumnVisible));

		this.LinkProperty(nameof(this.SelectedSecurity), nameof(this.AmountColumnHeader));
		this.LinkProperty(nameof(this.SelectedTransaction), nameof(this.IsTransactionDetailsVisible));

		this.OnSelectedAccountChanged();

		this.UpdateBalances();
		this.FillInMissingAlternateCurrencyValuesAsync(this.Transactions.ToList(), CancellationToken.None).Forget();
		this.Transactions.CollectionChanged += this.Transactions_CollectionChanged;
	}

	public string Title => "History";

	public SyncProgressData SyncProgress { get; }

	public SortedObservableCollection<TransactionViewModel> Transactions { get; } = new(TransactionViewModel.DateComparer.Instance);

	public string WhenColumnHeader => "When";

	public string AmountColumnHeader => this.SelectedSecurity.TickerSymbol;

	public string AlternateAmountColumnHeader => this.alternateSecurity.TickerSymbol;

	public bool IsAlternateAmountColumnVisible => this.isAlternateAmountColumnVisible.Value;

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

	public bool ExchangeRateExplanationIsVisible => this.exchangeRateExplanationIsVisible.Value;

	public ReactiveCommand<Unit, Unit> HideExchangeRateExplanationCommand { get; }

	public string HideExchangeRateExplanationCommandCaption => "Got it";

	protected override void OnSelectedAccountChanged()
	{
		base.OnSelectedAccountChanged();

		this.alternateSecurity = this.ViewModelServices.Settings.AlternateCurrency;

		this.transactionsSubscription?.Dispose();
		this.Transactions.Clear();
		this.transactionsSubscription = null;
		if (this.SelectedAccount is not null)
		{
			TradingPair tradingPair = new(this.alternateSecurity, this.SelectedSecurity);
			this.transactionsSubscription = WrapModels<ReadOnlyObservableCollection<ZcashTransaction>, ZcashTransaction, TransactionViewModel>(
				this.SelectedAccount.Transactions,
				this.Transactions,
				model => new TransactionViewModel(tradingPair, model, this));
		}
	}

	private async ValueTask FillInMissingAlternateCurrencyValuesAsync(IEnumerable<TransactionViewModel> transactions, CancellationToken cancellationToken)
	{
		TradingPair tradingPair = new(this.alternateSecurity, this.SelectedSecurity);
		foreach (TransactionViewModel tx in transactions)
		{
			if (tx.AlternateAmount is null && tx.When is not null)
			{
				ExchangeRate? rate = await this.ViewModelServices.ExchangeData.GetExchangeRateAsync(this.ViewModelServices.HistoricalExchangeRateProvider, tx.When.Value, tradingPair, cancellationToken);
				if (rate is not null)
				{
					tx.AlternateAmount = tx.Amount * rate;
				}
			}
		}
	}

	private void Transactions_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
	{
		if (e.NewStartingIndex >= 0)
		{
			if (e.OldStartingIndex >= 0)
			{
				this.UpdateBalances(Math.Min(e.NewStartingIndex, e.OldStartingIndex));
			}
			else
			{
				this.UpdateBalances(e.NewStartingIndex);
			}
		}
		else if (e.OldStartingIndex >= 0)
		{
			this.UpdateBalances(e.OldStartingIndex);
		}

		// Fill in missing alternate currency values.
		if (e.NewItems is not null)
		{
			this.FillInMissingAlternateCurrencyValuesAsync(e.NewItems.Cast<TransactionViewModel>(), CancellationToken.None).Forget();
		}
	}

	private void UpdateBalances(int startIndex = 0)
	{
		SecurityAmount runningBalance = startIndex > 0 ? this.Transactions[startIndex - 1].RunningBalance : this.SelectedSecurity.Amount(0);
		for (int i = startIndex; i < this.Transactions.Count; i++)
		{
			this.Transactions[i].RunningBalance = runningBalance += this.Transactions[i].Amount;
		}
	}
}
