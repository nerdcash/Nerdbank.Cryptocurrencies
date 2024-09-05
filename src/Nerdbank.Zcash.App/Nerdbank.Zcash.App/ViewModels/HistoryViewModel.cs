// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using DynamicData;
using Microsoft.VisualStudio.Threading;
using Nerdbank.Cryptocurrencies;
using Nerdbank.Cryptocurrencies.Exchanges;

namespace Nerdbank.Zcash.App.ViewModels;

public class HistoryViewModel : ViewModelBaseWithAccountSelector, IHasTitle
{
	private Security? alternateSecurity;
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
			MockTx(-0.5m, "Hot Chocolate", TimeSpan.FromDays(35), 100, "e5e259b8ef7f0cca708031ab0f10e2a3aa48e069a0817d3a54f71c7f56e0110d", "Red Rock Cafe"),
			MockTx(1.2345m, "For the pizza", TimeSpan.FromDays(200), 50, "9c1952fbaf5389fa8c36c45f17b2e303c33a9074dee8d90c694ee14112e0f46d", "Andrew Arnott"),
			MockTx(2m, "Paycheck", TimeSpan.FromDays(2), 200, "4e5f72b5eb58018daf506a13a5ccd9cb6b7657fd9f9ac4a8c297a51b5499ed9b", "Employer"),
			MockTx(13m, "Paycheck", TimeSpan.FromDays(75), blockNumber: null, "4e5f72b5eb58018daf506a13a5ccd9cb6b7657fd9f9ac4a8c297a51b5499ed9b", "Employer"),
			MockTx(-8m, "Unmined", TimeSpan.FromDays(5), blockNumber: null, "4e5f72b5eb58018daf506a13a5ccd9cb6b7657fd9f9ac4a8c297a51b5499ed9b", "Employer - Unmined", expiredUnmined: true),
		});

		TransactionViewModel MockTx(decimal amount, string memo, TimeSpan age, uint? blockNumber, string txid, string otherPartyName, bool expiredUnmined = false)
		{
			ImmutableArray<ZcashTransaction.LineItem> sends = amount < 0
				? [new ZcashTransaction.LineItem { Amount = -amount, Memo = Memo.FromMessage(memo), ToAddress = ZcashAddress.Decode("t1N7bGKWqoWVrv4XGSzrfUsoKkCxNFAutQZ") }]
				: [];
			ImmutableArray<ZcashTransaction.LineItem> receives = amount > 0
				? [new ZcashTransaction.LineItem { Amount = amount, Memo = Memo.FromMessage(memo), ToAddress = ZcashAddress.Decode("t1XkdzRXnCguCQtrigDUtgyz5SVtLYaAXBi") }]
				: [];
			return new TransactionViewModel(
				this.SelectedSecurity,
				Security.USD,
				new ZcashTransaction
				{
					BlockNumber = blockNumber,
					ExpiredUnmined = expiredUnmined,
					IsIncoming = amount > 0,
					Fee = amount > 0 ? null : 0.0001m,
					TransactionId = TxId.Parse(txid),
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

		this.exchangeRateExplanationIsVisible = this.WhenAnyValue(
			vm => vm.ViewModelServices.Settings.ExchangeRatePerTransactionHasBeenDismissed,
			vm => vm.SelectedSecurity.IsTestNet,
			vm => vm.Transactions.Count,
			(dismissed, isTestNet, txCount) => !dismissed && !isTestNet && txCount > 0)
			.ToProperty(this, nameof(this.ExchangeRateExplanationIsVisible));

		this.isAlternateAmountColumnVisible = this.WhenAnyValue(
			vm => vm.SelectedSecurity,
			vm => vm.ViewModelServices.Settings.AlternateCurrency,
			(s, ac) => !s.IsTestNet && ac is not null).ToProperty(this, nameof(this.IsAlternateNetChangeColumnVisible));

		this.LinkProperty(nameof(this.SelectedSecurity), nameof(this.AmountColumnHeader));
		this.LinkProperty(nameof(this.SelectedTransaction), nameof(this.IsTransactionDetailsVisible));
		this.LinkProperty(nameof(this.ShowProtocolDetails), nameof(this.IsBlockNumberVisible));
		this.LinkProperty(nameof(this.ShowProtocolDetails), nameof(this.IsTransactionIdVisible));

		this.WhenAnyValue(vm => vm.ViewModelServices.Settings.ShowProtocolDetails).Subscribe(_ => this.RaisePropertyChanged(nameof(this.ShowProtocolDetails)));

		this.OnSelectedAccountChanged();

		this.UpdateBalances();
		this.FillInMissingAlternateCurrencyValuesAsync(this.Transactions.ToList(), CancellationToken.None).Forget();
		this.Transactions.CollectionChanged += this.Transactions_CollectionChanged;
	}

	public string Title => HistoryStrings.Title;

	// When changing the sort order, be sure to update the UpdateBalances method (and maybe its callers)
	// to walk the collection in the new order.
	public SortedObservableCollection<TransactionViewModel> Transactions { get; } = new(TransactionChronologicalComparer.NewestToOldest);

	public string ShowProtocolDetailsCaption => HistoryStrings.ShowProtocolDetailsCaption;

	public bool ShowProtocolDetails
	{
		get => this.ViewModelServices.Settings.ShowProtocolDetails;
		set => this.ViewModelServices.Settings.ShowProtocolDetails = value;
	}

	public bool IsBlockNumberVisible => this.ShowProtocolDetails;

	public bool IsTransactionIdVisible => this.ShowProtocolDetails;

	public string WhenColumnHeader => HistoryStrings.WhenColumnHeader;

	public string AmountColumnHeader => this.SelectedSecurity.TickerSymbol;

	public string? AlternateNetChangeColumnHeader => this.alternateSecurity?.TickerSymbol;

	public bool IsAlternateNetChangeColumnVisible => this.isAlternateAmountColumnVisible.Value;

	public string OtherPartyNameColumnHeader => HistoryStrings.OtherPartyNameColumnHeader;

	public string MemoColumnHeader => HistoryStrings.MemoColumnHeader;

	public string RunningBalanceColumnHeader => HistoryStrings.RunningBalanceColumnHeader;

	public TransactionViewModel? SelectedTransaction
	{
		get => this.selectedTransaction;
		set => this.RaiseAndSetIfChanged(ref this.selectedTransaction, value);
	}

	public bool IsTransactionDetailsVisible => this.SelectedTransaction is not null;

	public string ExchangeRateExplanation => HistoryStrings.ExchangeRateExplanation;

	public bool ExchangeRateExplanationIsVisible => this.exchangeRateExplanationIsVisible.Value;

	public ReactiveCommand<Unit, Unit> HideExchangeRateExplanationCommand { get; }

	public string HideExchangeRateExplanationCommandCaption => HistoryStrings.HideExchangeRateExplanationCommandCaption;

	public void DeleteTransaction(TransactionViewModel transaction)
	{
		this.SelectedAccount?.RemoveTransaction(transaction.Model);
	}

	/// <summary>
	/// Called by a <see cref="TransactionViewModel"/> when its <see cref="TransactionViewModel.NetChange"/> property changes
	/// so the list view can be updated to reflect altered running balances.
	/// </summary>
	/// <param name="transaction">The changed transaction.</param>
	internal void OnTransactionNetChange(TransactionViewModel transaction)
	{
		int index = this.Transactions.IndexOf(transaction);
		if (index >= 0)
		{
			this.UpdateBalances(index);
		}
	}

	protected override void OnSelectedAccountChanged()
	{
		base.OnSelectedAccountChanged();

		this.alternateSecurity = this.ViewModelServices.Settings.AlternateCurrency;

		this.transactionsSubscription?.Dispose();
		this.Transactions.Clear();
		this.transactionsSubscription = null;
		if (this.SelectedAccount is not null)
		{
			this.transactionsSubscription = WrapModels<ReadOnlyObservableCollection<ZcashTransaction>, ZcashTransaction, TransactionViewModel>(
				this.SelectedAccount.Transactions,
				this.Transactions,
				model => new TransactionViewModel(this.SelectedSecurity, this.alternateSecurity, model, this));
		}
	}

	private async ValueTask FillInMissingAlternateCurrencyValuesAsync(IEnumerable<TransactionViewModel> transactions, CancellationToken cancellationToken)
	{
		if (this.alternateSecurity is null)
		{
			return;
		}

		TradingPair tradingPair = new(this.alternateSecurity, this.SelectedSecurity);
		foreach (TransactionViewModel tx in transactions)
		{
			if (tx.AlternateNetChange is null && tx.When is not null)
			{
				ExchangeRate? rate = await this.ViewModelServices.ExchangeData.GetExchangeRateAsync(this.ViewModelServices.HistoricalExchangeRateProvider, tx.When.Value, tradingPair, cancellationToken);
				if (rate is not null)
				{
					tx.AlternateNetChange = tx.NetChange * rate;
				}
			}
		}
	}

	private void Transactions_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
	{
		// We should only update transactions with invalidated balances.
		// Remember that we sort newest to oldest, so the first transaction in the list is the most recent.
		// So we first seek to know the index to the *oldest* impacted transaction,
		// and we'll consider it and all newer transactions as invalidated.
		int? indexOfOldestImpactedTransaction = null;
		if (e.NewStartingIndex >= 0 && e.NewItems is not null && e.OldStartingIndex < 0)
		{
			// The oldest impacted transaction is the last in the set that was just added.
			indexOfOldestImpactedTransaction = e.NewStartingIndex + e.NewItems.Count - 1;
		}
		else if (e.OldStartingIndex >= 0 && e.NewStartingIndex < 0)
		{
			// The oldest impacted transaction is the one just above the set that was removed.
			indexOfOldestImpactedTransaction = e.OldStartingIndex - 1;
		}

		this.UpdateBalances(indexOfOldestImpactedTransaction >= 0 ? indexOfOldestImpactedTransaction : null);

		// Fill in missing alternate currency values.
		if (e.NewItems is not null)
		{
			this.FillInMissingAlternateCurrencyValuesAsync(e.NewItems.Cast<TransactionViewModel>(), CancellationToken.None).Forget();
		}
	}

	private void UpdateBalances(int? indexToOldestInvalidatedTransaction = null)
	{
		if (this.Transactions.Count == 0)
		{
			return;
		}

		indexToOldestInvalidatedTransaction ??= this.Transactions.Count - 1;
		SecurityAmount runningBalance = indexToOldestInvalidatedTransaction.Value + 1 < this.Transactions.Count
			? this.Transactions[indexToOldestInvalidatedTransaction.Value + 1].RunningBalance
			: this.SelectedSecurity.Amount(0);

		for (int i = indexToOldestInvalidatedTransaction.Value; i >= 0; i--)
		{
			if (!this.Transactions[i].ExpiredUnmined)
			{
				runningBalance += this.Transactions[i].NetChange;
			}

			this.Transactions[i].RunningBalance = runningBalance;
		}
	}
}
