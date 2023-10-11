// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Cryptocurrencies.Exchanges;

namespace Nerdbank.Zcash.App.ViewModels;

public class BalanceViewModel : ViewModelBase
{
	private SecurityAmount balance;
	private SecurityAmount immatureIncome;
	private SecurityAmount unconfirmedIncome;
	private SecurityAmount spendableBalance;
	private SecurityAmount unspendableChange;

	[Obsolete("For design-time use only.", error: true)]
	public BalanceViewModel()
	{
		this.CommonConstruction();

		this.balance = new(10.123m, this.ZcashSecurity);
		this.immatureIncome = new(0.5m, this.ZcashSecurity);
		this.unconfirmedIncome = new(1.2m, this.ZcashSecurity);
		this.spendableBalance = new(10.100m, this.ZcashSecurity);
		this.unspendableChange = new(0.023m, this.ZcashSecurity);
	}

	public SyncProgressData SyncProgress { get; } = new SyncProgressData();

	public SecurityAmount Balance
	{
		get => this.balance;
		set => this.RaiseAndSetIfChanged(ref this.balance, value);
	}

	public string BalanceCaption => "Balance";

	public bool IsImmatureIncomeVisible => this.ImmatureIncome.Amount > 0;

	public SecurityAmount ImmatureIncome
	{
		get => this.immatureIncome;
		set => this.RaiseAndSetIfChanged(ref this.immatureIncome, value);
	}

	public string ImmatureIncomeCaption => "📩 Incoming (immature)";

	public string ImmatureIncomeExplanation => "Zcash has been sent to you and confirmed, but is not yet available to spend. This stage can last several minutes.";

	public SecurityAmount UnconfirmedIncome
	{
		get => this.unconfirmedIncome;
		set => this.RaiseAndSetIfChanged(ref this.unconfirmedIncome, value);
	}

	public bool IsUnconfirmedIncomeVisible => this.UnconfirmedIncome.Amount > 0;

	public string UnconfirmedIncomeCaption => "Incoming (unconfirmed)";

	public string UnconfirmedIncomeExplanation => "Zcash has been sent to you but has not yet been confirmed. Unconfirmed funds aren't guaranteed to be yours yet. This usually clears up in a minute or two.";

	public bool IsBalanceBreakdownVisible => this.SpendableBalance != this.Balance;

	public SecurityAmount SpendableBalance
	{
		get => this.spendableBalance;
		set => this.RaiseAndSetIfChanged(ref this.spendableBalance, value);
	}

	public string SpendableBalanceCaption => "💵 Spendable";

	public string SpendableBalanceExplanation => "This is the Zcash you can spend right now.";

	public bool IsUnspendableChangeVisible => this.UnspendableChange.Amount > 0;

	public SecurityAmount UnspendableChange
	{
		get => this.unspendableChange;
		set => this.RaiseAndSetIfChanged(ref this.unspendableChange, value);
	}

	public string UnspendableChangeCaption => "🪢 Tied up";

	public string UnspendableChangeExplanation => "Recent spends can tie up some of your Zcash for a few minutes.";

	private void CommonConstruction()
	{
		this.LinkProperty(nameof(this.Balance), nameof(this.IsBalanceBreakdownVisible));
		this.LinkProperty(nameof(this.ImmatureIncome), nameof(this.IsImmatureIncomeVisible));
		this.LinkProperty(nameof(this.UnconfirmedIncome), nameof(this.IsUnconfirmedIncomeVisible));
		this.LinkProperty(nameof(this.SpendableBalance), nameof(this.IsBalanceBreakdownVisible));
		this.LinkProperty(nameof(this.UnspendableChange), nameof(this.IsUnspendableChangeVisible));
	}
}
