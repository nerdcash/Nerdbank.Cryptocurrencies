// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Cryptocurrencies.Exchanges;

namespace Nerdbank.Zcash.App.ViewModels;

public class BalanceViewModel : ViewModelBaseWithAccountSelector, IHasTitle
{
	private ObservableAsPropertyHelper<SecurityAmount?> balance;
	private ObservableAsPropertyHelper<SecurityAmount?> spendableBalance;
	private ObservableAsPropertyHelper<bool> isBalanceBreakdownVisible;

	private ObservableAsPropertyHelper<SecurityAmount?> anticipatedFees;
	private ObservableAsPropertyHelper<bool> isAnticipatedFeesVisible;

	private ObservableAsPropertyHelper<bool> isUnconfirmedIncomeVisible;
	private ObservableAsPropertyHelper<SecurityAmount?> unconfirmedIncome;

	private ObservableAsPropertyHelper<SecurityAmount?> unspendableChange;
	private ObservableAsPropertyHelper<bool> isUnspendableChangeVisible;

	private ObservableAsPropertyHelper<SecurityAmount?> immatureIncome;
	private ObservableAsPropertyHelper<bool> isImmatureIncomeVisible;

	[Obsolete("For design-time use only.", error: true)]
	public BalanceViewModel()
		: this(new DesignTimeViewModelServices())
	{
		Account account = this.SelectedAccount ?? throw new InvalidOperationException();

		account.AnticipatedFees = -0.103m;
		account.ImmatureIncome = 0.5m;
		account.UnconfirmedBalance = 1.2m;
		account.SpendableBalance = 10.100m;
		account.UnspendableChange = 0.023m;
		account.Balance = account.SpendableBalance + account.UnspendableChange + account.ImmatureIncome + account.AnticipatedFees;
	}

	public BalanceViewModel(IViewModelServices viewModelServices)
		: base(viewModelServices)
	{
		this.SyncProgress = new SyncProgressData(this);

		this.balance = this.WhenAnyValue(
			vm => vm.SelectedAccount,
			vm => vm.SelectedAccount!.Balance,
			(a, b) => a?.Network.AsSecurity().Amount(b)).ToProperty(this, nameof(this.Balance));
		this.spendableBalance = this.WhenAnyValue(
			vm => vm.SelectedAccount,
			vm => vm.SelectedAccount!.SpendableBalance,
			(a, b) => a?.Network.AsSecurity().Amount(b)).ToProperty(this, nameof(this.SpendableBalance));
		this.isBalanceBreakdownVisible = this.WhenAnyValue(
			vm => vm.Balance,
			vm => vm.SpendableBalance,
			(balance, spendableBalance) => balance != spendableBalance).ToProperty(this, nameof(this.IsBalanceBreakdownVisible));

		this.unspendableChange = this.WhenAnyValue(
			vm => vm.SelectedAccount,
			vm => vm.SelectedAccount!.UnspendableChange,
			(a, b) => a?.Network.AsSecurity().Amount(b)).ToProperty(this, nameof(this.UnspendableChange));
		this.isUnspendableChangeVisible = this.WhenAnyValue(
			vm => vm.UnspendableChange,
			c => c?.Amount > 0).ToProperty(this, nameof(this.IsUnspendableChangeVisible));

		this.unconfirmedIncome = this.WhenAnyValue(
			vm => vm.SelectedAccount,
			vm => vm.SelectedAccount!.UnconfirmedBalance,
			(a, b) => a?.Network.AsSecurity().Amount(b)).ToProperty(this, nameof(this.UnconfirmedIncome));
		this.isUnconfirmedIncomeVisible = this.WhenAnyValue(
			vm => vm.UnconfirmedIncome,
			i => i?.Amount > 0).ToProperty(this, nameof(this.IsUnconfirmedIncomeVisible));

		this.anticipatedFees = this.WhenAnyValue(
			vm => vm.SelectedAccount,
			vm => vm.SelectedAccount!.AnticipatedFees,
			(a, b) => a?.Network.AsSecurity().Amount(b)).ToProperty(this, nameof(this.AnticipatedFees));
		this.isAnticipatedFeesVisible = this.WhenAnyValue(
			vm => vm.AnticipatedFees,
			f => f?.Amount < 0).ToProperty(this, nameof(this.IsAnticipatedFeesVisible));

		this.immatureIncome = this.WhenAnyValue(
			vm => vm.SelectedAccount,
			vm => vm.SelectedAccount!.ImmatureIncome,
			(a, b) => a?.Network.AsSecurity().Amount(b)).ToProperty(this, nameof(this.ImmatureIncome));
		this.isImmatureIncomeVisible = this.WhenAnyValue(
			vm => vm.ImmatureIncome,
			i => i?.Amount > 0).ToProperty(this, nameof(this.IsImmatureIncomeVisible));
	}

	public SyncProgressData SyncProgress { get; }

	public string Title => "Balance";

	public SecurityAmount? Balance => this.balance.Value;

	public string BalanceCaption => "💰 Balance";

	public string AnticipatedFeesCaption => "🪙 Anticipated fees";

	public bool IsAnticipatedFeesVisible => this.isAnticipatedFeesVisible.Value;

	public SecurityAmount? AnticipatedFees => this.anticipatedFees.Value;

	public string AnticipatedFeesExplanation => "This is the estimated portion of your balance that will go to fees when you spend your Zcash.";

	public bool IsImmatureIncomeVisible => this.isImmatureIncomeVisible.Value;

	public SecurityAmount? ImmatureIncome => this.immatureIncome.Value;

	public string ImmatureIncomeCaption => "📩 Immature";

	public string ImmatureIncomeExplanation => "Zcash has been sent to you and confirmed, but is not yet available to spend. This stage can last several minutes.";

	public SecurityAmount? UnconfirmedIncome => this.unconfirmedIncome.Value;

	public bool IsUnconfirmedIncomeVisible => this.isUnconfirmedIncomeVisible.Value;

	public string UnconfirmedIncomeCaption => "📥 Incoming";

	public string UnconfirmedIncomeExplanation => "Zcash has been sent to you but has not yet been confirmed. Unconfirmed funds aren't guaranteed to be yours yet. This usually clears up in a minute or two.";

	public bool IsBalanceBreakdownVisible => this.isBalanceBreakdownVisible.Value;

	public SecurityAmount? SpendableBalance => this.spendableBalance.Value;

	public string SpendableBalanceCaption => "💵 Spendable";

	public string SpendableBalanceExplanation => "This is the Zcash you can spend right now.";

	public bool IsUnspendableChangeVisible => this.isUnspendableChangeVisible.Value;

	public SecurityAmount? UnspendableChange => this.unspendableChange.Value;

	public string UnspendableChangeCaption => "🪢 Tied up";

	public string UnspendableChangeExplanation => "Recent spends can tie up some of your Zcash for a few minutes.";
}
