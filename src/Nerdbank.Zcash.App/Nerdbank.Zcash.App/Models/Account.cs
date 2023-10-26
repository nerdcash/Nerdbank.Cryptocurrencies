// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Cryptocurrencies.Exchanges;

namespace Nerdbank.Zcash.App.Models;

public class Account : ReactiveObject
{
	private readonly ObservableAsPropertyHelper<SecurityAmount> securityBalance;
	private string name = string.Empty;
	private decimal balance;

	public Account(ZcashAccount account, HDWallet? memberOf)
	{
		this.ZcashAccount = account;
		this.MemberOf = memberOf;

		this.securityBalance = this.WhenAnyValue(
			vm => vm.Balance,
			balance => this.Network.AsSecurity().Amount(balance))
			.ToProperty(this, nameof(this.SecurityBalance));
	}

	public ZcashAccount ZcashAccount { get; }

	public ZcashNetwork Network => this.ZcashAccount.Network;

	public HDWallet? MemberOf { get; }

	public string Name
	{
		get => this.name;
		set => this.RaiseAndSetIfChanged(ref this.name, value);
	}

	public decimal Balance
	{
		get => this.balance;
		set => this.RaiseAndSetIfChanged(ref this.balance, value);
	}

	public SecurityAmount SecurityBalance => this.securityBalance.Value;
}
