// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using Nerdbank.Cryptocurrencies.Exchanges;

namespace Nerdbank.Zcash.App.ViewModels;

public class BalanceViewModel : ViewModelBaseWithAccountSelector, IHasTitle
{
	private SecurityAmount immatureIncome;
	private SecurityAmount unconfirmedIncome;
	private SecurityAmount spendableBalance;
	private SecurityAmount unspendableChange;
	private SecurityAmount anticipatedFees;

	[Obsolete("For design-time use only.", error: true)]
	public BalanceViewModel()
		: this(new DesignTimeViewModelServices())
	{
		Security security = this.ViewModelServices.SelectedAccount!.Network.AsSecurity();

		this.anticipatedFees = new(-0.103m, security);
		this.immatureIncome = new(0.5m, security);
		this.unconfirmedIncome = new(1.2m, security);
		this.spendableBalance = new(10.100m, security);
		this.unspendableChange = new(0.023m, security);
	}

	public BalanceViewModel(IViewModelServices viewModelServices)
		: base(viewModelServices)
	{
		this.SelectedAccount = this.Accounts.FirstOrDefault(viewModelServices.SelectedAccount);

		this.LinkProperty(nameof(this.SpendableBalance), nameof(this.IsBalanceBreakdownVisible));
		this.LinkProperty(nameof(this.Balance), nameof(this.IsBalanceBreakdownVisible));

		this.LinkProperty(nameof(this.SpendableBalance), nameof(this.Balance));
		this.LinkProperty(nameof(this.UnspendableChange), nameof(this.Balance));
		this.LinkProperty(nameof(this.ImmatureIncome), nameof(this.Balance));
		this.LinkProperty(nameof(this.AnticipatedFees), nameof(this.Balance));

		this.LinkProperty(nameof(this.ImmatureIncome), nameof(this.IsImmatureIncomeVisible));
		this.LinkProperty(nameof(this.UnconfirmedIncome), nameof(this.IsUnconfirmedIncomeVisible));
		this.LinkProperty(nameof(this.UnspendableChange), nameof(this.IsUnspendableChangeVisible));
		this.LinkProperty(nameof(this.AnticipatedFees), nameof(this.IsAnticipatedFeesVisible));
	}

	public SyncProgressData SyncProgress { get; } = new SyncProgressData();

	public string Title => "Balance";

	public SecurityAmount Balance => this.spendableBalance + this.unspendableChange + this.immatureIncome + this.anticipatedFees;

	public string BalanceCaption => "💰 Balance";

	public string AnticipatedFeesCaption => "🪙 Anticipated fees";

	public bool IsAnticipatedFeesVisible => this.AnticipatedFees.Amount < 0;

	public SecurityAmount AnticipatedFees
	{
		get => this.anticipatedFees;
		set => this.RaiseAndSetIfChanged(ref this.anticipatedFees, value);
	}

	public string AnticipatedFeesExplanation => "This is the estimated portion of your balance that will go to fees when you spend your Zcash.";

	public bool IsImmatureIncomeVisible => this.ImmatureIncome.Amount > 0;

	public SecurityAmount ImmatureIncome
	{
		get => this.immatureIncome;
		set => this.RaiseAndSetIfChanged(ref this.immatureIncome, value);
	}

	public string ImmatureIncomeCaption => "📩 Immature";

	public string ImmatureIncomeExplanation => "Zcash has been sent to you and confirmed, but is not yet available to spend. This stage can last several minutes.";

	public SecurityAmount UnconfirmedIncome
	{
		get => this.unconfirmedIncome;
		set => this.RaiseAndSetIfChanged(ref this.unconfirmedIncome, value);
	}

	public bool IsUnconfirmedIncomeVisible => this.UnconfirmedIncome.Amount > 0;

	public string UnconfirmedIncomeCaption => "📥 Incoming";

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
}
