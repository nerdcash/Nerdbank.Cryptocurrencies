// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash.App.ViewModels;

public class BalanceViewModel : ViewModelBase
{
	private decimal balance = 10.123m;
	private decimal immatureIncome = 0.5m;
	private decimal unconfirmedIncome = 1.2m;
	private decimal spendableBalance = 10.100m;
	private decimal unspendableChange = 0.023m;

	public BalanceViewModel()
	{
		this.LinkProperty(nameof(this.Balance), nameof(this.BalanceFormatted));
		this.LinkProperty(nameof(this.Balance), nameof(this.IsBalanceBreakdownVisible));

		this.LinkProperty(nameof(this.ImmatureIncome), nameof(this.IsImmatureIncomeVisible));
		this.LinkProperty(nameof(this.ImmatureIncome), nameof(this.ImmatureIncomeFormatted));

		this.LinkProperty(nameof(this.UnconfirmedIncome), nameof(this.IsUnconfirmedIncomeVisible));
		this.LinkProperty(nameof(this.UnconfirmedIncome), nameof(this.UnconfirmedIncomeFormatted));

		this.LinkProperty(nameof(this.SpendableBalance), nameof(this.IsBalanceBreakdownVisible));
		this.LinkProperty(nameof(this.SpendableBalance), nameof(this.SpendableBalanceFormatted));
	}

	public SyncProgressData SyncProgress { get; } = new SyncProgressData();

	public decimal Balance
	{
		get => this.balance;
		set => this.RaiseAndSetIfChanged(ref this.balance, value);
	}

	public string BalanceCaption => "Balance";

	public ZcashAmountFormatted BalanceFormatted => new(this.Balance, this.Network);

	public bool IsImmatureIncomeVisible => this.ImmatureIncome != 0;

	public decimal ImmatureIncome
	{
		get => this.immatureIncome;
		set => this.RaiseAndSetIfChanged(ref this.immatureIncome, value);
	}

	public string ImmatureIncomeCaption => "Incoming (immature)";

	public ZcashAmountFormatted ImmatureIncomeFormatted => new(this.ImmatureIncome, this.Network);

	public string ImmatureIncomeExplanation => "Zcash has been sent to you and confirmed, but is not yet available to spend. This stage can last several minutes.";

	public decimal UnconfirmedIncome
	{
		get => this.unconfirmedIncome;
		set => this.RaiseAndSetIfChanged(ref this.unconfirmedIncome, value);
	}

	public bool IsUnconfirmedIncomeVisible => this.UnconfirmedIncome != 0;

	public string UnconfirmedIncomeCaption => "Incoming (unconfirmed)";

	public ZcashAmountFormatted UnconfirmedIncomeFormatted => new(this.UnconfirmedIncome, this.Network);

	public string UnconfirmedIncomeExplanation => "Zcash has been sent to you but has not yet been confirmed. Unconfirmed funds aren't guaranteed to be yours yet. This usually clears up in a minute or two.";

	public bool IsBalanceBreakdownVisible => this.SpendableBalance != this.Balance;

	public decimal SpendableBalance
	{
		get => this.spendableBalance;
		set => this.RaiseAndSetIfChanged(ref this.spendableBalance, value);
	}

	public string SpendableBalanceCaption => "Spendable";

	public ZcashAmountFormatted SpendableBalanceFormatted => new(this.SpendableBalance, this.Network);

	public string SpendableBalanceExplanation => "This is the Zcash you can spend right now.";

	public decimal UnspendableChange
	{
		get => this.unspendableChange;
		set => this.RaiseAndSetIfChanged(ref this.unspendableChange, value);
	}

	public string UnspendableChangeCaption => "🪢 Tied up";

	public ZcashAmountFormatted UnspendableChangeFormatted => new(this.UnspendableChange, this.Network);

	public string UnspendableChangeExplanation => "Recent spends can tie up some of your Zcash for a few minutes.";
}
