// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Collections.ObjectModel;
using Nerdbank.Cryptocurrencies;

namespace Nerdbank.Zcash.App.ViewModels;

public class HistoryViewModel : ViewModelBaseWithAccountSelector, IHasTitle
{
	private IDisposable? transactionsSubscription;
	private ObservableAsPropertyHelper<bool> exchangeRatePerTransactionHasBeenDismissed;
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
			ImmutableArray<Transaction.SendItem> sends = amount < 0
				? ImmutableArray.Create(new Transaction.SendItem { Amount = amount, Memo = Memo.FromMessage(memo) })
				: ImmutableArray<Transaction.SendItem>.Empty;
			ImmutableArray<Transaction.RecvItem> receives = amount > 0
				? ImmutableArray.Create(new Transaction.RecvItem { Amount = amount, Memo = Memo.FromMessage(memo) })
				: ImmutableArray<Transaction.RecvItem>.Empty;
			return new TransactionViewModel(
				new ZcashTransaction
				{
					IsIncoming = amount > 0,
					TransactionId = txid,
					When = DateTimeOffset.UtcNow - age,
					OtherPartyName = otherPartyName,
					Security = this.SelectedSecurity,
					SendItems = sends,
					RecvItems = receives,
				},
				this.ViewModelServices);
		}
	}

	public HistoryViewModel(IViewModelServices viewModelServices)
		: base(viewModelServices)
	{
		this.HideExchangeRateExplanationCommand = ReactiveCommand.Create(() => { this.ViewModelServices.Settings.ExchangeRatePerTransactionHasBeenDismissed = true; });

		this.SyncProgress = new SyncProgressData(this);

		this.exchangeRatePerTransactionHasBeenDismissed = viewModelServices.Settings.WhenAnyValue(s => s.ExchangeRatePerTransactionHasBeenDismissed, d => !d)
			.ToProperty(this, nameof(this.ExchangeRateExplanationIsVisible));

		this.LinkProperty(nameof(this.SelectedSecurity), nameof(this.AmountColumnHeader));
		this.LinkProperty(nameof(this.SelectedTransaction), nameof(this.IsTransactionDetailsVisible));

		this.OnSelectedAccountChanged();

		this.UpdateBalances();
		this.Transactions.CollectionChanged += this.Transactions_CollectionChanged;
	}

	public string Title => "History";

	public SyncProgressData SyncProgress { get; }

	public SortedObservableCollection<TransactionViewModel> Transactions { get; } = new(TransactionViewModel.DateComparer.Instance);

	public string WhenColumnHeader => "When";

	public string AmountColumnHeader => this.SelectedSecurity.TickerSymbol;

	public string AlternateAmountColumnHeader => "Alternate";

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

	protected override void OnSelectedAccountChanged()
	{
		base.OnSelectedAccountChanged();

		this.transactionsSubscription?.Dispose();
		this.Transactions.Clear();
		this.transactionsSubscription = null;
		if (this.SelectedAccount is not null)
		{
			this.transactionsSubscription = WrapModels<ReadOnlyObservableCollection<ZcashTransaction>, ZcashTransaction, TransactionViewModel>(
				this.SelectedAccount.Transactions,
				this.Transactions,
				model => new TransactionViewModel(model, this.ViewModelServices));
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
