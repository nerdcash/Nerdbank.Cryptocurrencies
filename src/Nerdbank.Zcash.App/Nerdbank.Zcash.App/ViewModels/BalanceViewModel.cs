// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Cryptocurrencies;
using Nerdbank.Cryptocurrencies.Exchanges;

namespace Nerdbank.Zcash.App.ViewModels;

public class BalanceViewModel : ViewModelBaseWithExchangeRate, IHasTitle
{
	private readonly ObservableAsPropertyHelper<bool> isSwitchCurrencyCommandVisible;

	private readonly ObservableAsPropertyHelper<SecurityAmount?> balance;
	private readonly ObservableAsPropertyHelper<SecurityAmount?> spendableBalance;
	private readonly ObservableAsPropertyHelper<bool> isBalanceBreakdownVisible;

	private readonly ObservableAsPropertyHelper<SecurityAmount?> anticipatedFees;
	private readonly ObservableAsPropertyHelper<bool> isAnticipatedFeesVisible;

	private readonly ObservableAsPropertyHelper<bool> isUnconfirmedIncomeVisible;
	private readonly ObservableAsPropertyHelper<SecurityAmount?> unconfirmedIncome;

	private readonly ObservableAsPropertyHelper<SecurityAmount?> unspendableChange;
	private readonly ObservableAsPropertyHelper<bool> isUnspendableChangeVisible;

	private readonly ObservableAsPropertyHelper<SecurityAmount?> immatureIncome;
	private readonly ObservableAsPropertyHelper<bool> isImmatureIncomeVisible;

	private readonly ObservableAsPropertyHelper<string> exchangeRateText;
	private readonly ObservableAsPropertyHelper<bool> exchangeRateVisible;

	private bool showAlternateCurrency;

	[Obsolete("For design-time use only.", error: true)]
	public BalanceViewModel()
		: this(new DesignTimeViewModelServices())
	{
		Account account = this.SelectedAccount ?? throw new InvalidOperationException();

		Security security = account.Network.AsSecurity();
		account.Balance = new AccountBalances
		{
			MinimumFees = security.Amount(-0.103m),
			ImmatureIncome = security.Amount(0.5m),
			Incoming = security.Amount(1.2m),
			Spendable = security.Amount(10.100m),
			ImmatureChange = security.Amount(0.023m),
			Dust = security.Amount(0.00000000m),
			IncomingDust = security.Amount(0.00000000m),
		};
	}

	public BalanceViewModel(IViewModelServices viewModelServices)
		: base(viewModelServices)
	{
		// For view-only accounts, we cannot perform auto-shielding.
		// So instead of representing unshielded funds as immature income, we include them in the "spendable" category.
		this.balance = this.WhenAnyValue(
			vm => vm.SelectedAccount,
			vm => vm.SelectedAccount!.Balance,
			vm => vm.ShowAlternateCurrency,
			vm => vm.ExchangeRate,
			(a, b, alt, x) => X(x, alt, b.MainBalance)).ToProperty(this, nameof(this.Balance));
		this.spendableBalance = this.WhenAnyValue(
			vm => vm.SelectedAccount,
			vm => vm.SelectedAccount!.Balance,
			vm => vm.ShowAlternateCurrency,
			vm => vm.ExchangeRate,
			(a, b, alt, x) => X(x, alt, ViewOrSpend(a, b.Spendable + b.ImmatureIncome, b.Spendable))).ToProperty(this, nameof(this.SpendableBalance));
		this.isBalanceBreakdownVisible = this.WhenAnyValue(
			vm => vm.Balance,
			vm => vm.SpendableBalance,
			(balance, spendableBalance) => balance != spendableBalance).ToProperty(this, nameof(this.IsBalanceBreakdownVisible));

		this.unspendableChange = this.WhenAnyValue(
			vm => vm.SelectedAccount,
			vm => vm.SelectedAccount!.Balance,
			vm => vm.ShowAlternateCurrency,
			vm => vm.ExchangeRate,
			(a, b, alt, x) => X(x, alt, b.ImmatureChange)).ToProperty(this, nameof(this.UnspendableChange));
		this.isUnspendableChangeVisible = this.WhenAnyValue(
			vm => vm.UnspendableChange,
			c => c?.Amount > 0).ToProperty(this, nameof(this.IsUnspendableChangeVisible));

		this.unconfirmedIncome = this.WhenAnyValue(
			vm => vm.SelectedAccount,
			vm => vm.SelectedAccount!.Balance,
			vm => vm.ShowAlternateCurrency,
			vm => vm.ExchangeRate,
			(a, b, alt, x) => X(x, alt, b.Incoming)).ToProperty(this, nameof(this.UnconfirmedIncome));
		this.isUnconfirmedIncomeVisible = this.WhenAnyValue(
			vm => vm.UnconfirmedIncome,
			i => i?.Amount > 0).ToProperty(this, nameof(this.IsUnconfirmedIncomeVisible));

		this.anticipatedFees = this.WhenAnyValue(
			vm => vm.SelectedAccount,
			vm => vm.SelectedAccount!.Balance,
			vm => vm.ShowAlternateCurrency,
			vm => vm.ExchangeRate,
			(a, b, alt, x) => X(x, alt, b.MinimumFees)).ToProperty(this, nameof(this.AnticipatedFees));
		this.isAnticipatedFeesVisible = this.WhenAnyValue(
			vm => vm.AnticipatedFees,
			f => f?.Amount < 0).ToProperty(this, nameof(this.IsAnticipatedFeesVisible));

		this.immatureIncome = this.WhenAnyValue(
			vm => vm.SelectedAccount,
			vm => vm.SelectedAccount!.Balance,
			vm => vm.ShowAlternateCurrency,
			vm => vm.ExchangeRate,
			(a, b, alt, x) => X(x, alt, ViewOrSpend(a, spend: b.ImmatureIncome))).ToProperty(this, nameof(this.ImmatureIncome));
		this.isImmatureIncomeVisible = this.WhenAnyValue(
			vm => vm.ImmatureIncome,
			i => i?.Amount > 0).ToProperty(this, nameof(this.IsImmatureIncomeVisible));

		this.isSwitchCurrencyCommandVisible = this.WhenAnyValue(
			vm => vm.SelectedAccount,
			a => a?.Network == ZcashNetwork.MainNet).ToProperty(this, nameof(this.IsSwitchCurrencyVisible));

		this.exchangeRateText = this.WhenAnyValue(
			vm => vm.ExchangeRate,
			rate => rate is null ? string.Empty : $"1 ZEC = {rate.Value.Normalized.Basis}").ToProperty(this, nameof(this.ExchangeRateText));

		this.exchangeRateVisible = this.WhenAnyValue(
			vm => vm.ExchangeRate,
			rate => rate.HasValue).ToProperty(this, nameof(this.ExchangeRateVisible));

		this.SwitchCurrencyCommand = ReactiveCommand.Create(this.SwitchCurrency);

		static SecurityAmount? X(ExchangeRate? exchangeRate, bool showAlternateCurrency, SecurityAmount nativeAmount) => showAlternateCurrency && exchangeRate.HasValue && nativeAmount.Security is not null ? nativeAmount * exchangeRate : nativeAmount;
		static SecurityAmount ViewOrSpend(Account? account, SecurityAmount? viewOnly = default, SecurityAmount? spend = default) => (account?.ZcashAccount.Spending is null ? viewOnly : spend) ?? account?.Network.AsSecurity().Amount(0) ?? default;
	}

	public string Title => BalanceStrings.Title;

	public SecurityAmount? Balance => this.balance.Value;

	public string BalanceCaption => BalanceStrings.BalanceCaption;

	public string AnticipatedFeesCaption => BalanceStrings.AnticipatedFeesCaption;

	public bool IsAnticipatedFeesVisible => this.isAnticipatedFeesVisible.Value;

	public SecurityAmount? AnticipatedFees => this.anticipatedFees.Value;

	public string AnticipatedFeesExplanation => BalanceStrings.AnticipatedFeesExplanation;

	public bool IsImmatureIncomeVisible => this.isImmatureIncomeVisible.Value;

	public SecurityAmount? ImmatureIncome => this.immatureIncome.Value;

	public string ImmatureIncomeCaption => BalanceStrings.ImmatureIncomeCaption;

	public string ImmatureIncomeExplanation => BalanceStrings.ImmatureIncomeExplanation;

	public SecurityAmount? UnconfirmedIncome => this.unconfirmedIncome.Value;

	public bool IsUnconfirmedIncomeVisible => this.isUnconfirmedIncomeVisible.Value;

	public string UnconfirmedIncomeCaption => BalanceStrings.UnconfirmedIncomeCaption;

	public string UnconfirmedIncomeExplanation => BalanceStrings.UnconfirmedIncomeExplanation;

	public bool IsBalanceBreakdownVisible => this.isBalanceBreakdownVisible.Value;

	public SecurityAmount? SpendableBalance => this.spendableBalance.Value;

	public string SpendableBalanceCaption => BalanceStrings.SpendableBalanceCaption;

	public string SpendableBalanceExplanation => BalanceStrings.SpendableBalanceExplanation;

	public bool IsUnspendableChangeVisible => this.isUnspendableChangeVisible.Value;

	public SecurityAmount? UnspendableChange => this.unspendableChange.Value;

	public string UnspendableChangeCaption => BalanceStrings.UnspendableChangeCaption;

	public string UnspendableChangeExplanation => BalanceStrings.UnspendableChangeExplanation;

	public bool ShowAlternateCurrency
	{
		get => this.showAlternateCurrency;
		set => this.RaiseAndSetIfChanged(ref this.showAlternateCurrency, value);
	}

	/// <summary>
	/// Gets the command that switches the view between Zcash and the user's selected alternate currency.
	/// </summary>
	public ReactiveCommand<Unit, Unit> SwitchCurrencyCommand { get; }

	public string SwitchCurrencyCaption => BalanceStrings.SwitchCurrencyCaption;

	public bool IsSwitchCurrencyVisible => this.isSwitchCurrencyCommandVisible.Value;

	public string ExchangeRateText => this.exchangeRateText.Value;

	public bool ExchangeRateVisible => this.exchangeRateVisible.Value;

	private void SwitchCurrency() => this.ShowAlternateCurrency = !this.ShowAlternateCurrency;
}
