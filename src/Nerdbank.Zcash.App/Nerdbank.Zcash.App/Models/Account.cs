// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MessagePack;
using Nerdbank.Cryptocurrencies.Exchanges;

namespace Nerdbank.Zcash.App.Models;

[MessagePackObject]
public class Account : ReactiveObject, IPersistableData
{
	private readonly ObservableAsPropertyHelper<SecurityAmount> securityBalance;
	private string? zingoWalletFileName;
	private string name = string.Empty;
	private decimal balance;
	private bool isDirty;

	public Account(ZcashAccount account)
	{
		this.ZcashAccount = account;

		this.securityBalance = this.WhenAnyValue(
			vm => vm.Balance,
			balance => this.Network.AsSecurity().Amount(balance))
			.ToProperty(this, nameof(this.SecurityBalance));

		this.MarkSelfDirtyOnPropertyChanged();
	}

	[IgnoreMember]
	public bool IsDirty
	{
		get => this.isDirty;
		set => this.RaiseAndSetIfChanged(ref this.isDirty, value);
	}

	[Key(0)]
	public ZcashAccount ZcashAccount { get; }

	[IgnoreMember]
	public ZcashNetwork Network => this.ZcashAccount.Network;

	[Key(1)]
	public string Name
	{
		get => this.name;
		set => this.RaiseAndSetIfChanged(ref this.name, value);
	}

	[IgnoreMember]
	public decimal Balance
	{
		get => this.balance;
		set => this.RaiseAndSetIfChanged(ref this.balance, value);
	}

	[IgnoreMember]
	public SecurityAmount SecurityBalance => this.securityBalance.Value;
}
