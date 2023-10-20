// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using Nerdbank.Cryptocurrencies;
using Nerdbank.Cryptocurrencies.Exchanges;

namespace Nerdbank.Zcash.App.ViewModels;

public class AccountViewModel : ViewModelBase
{
	private string name = string.Empty;
	private SecurityAmount balance;
	private bool areKeysRevealed;

	[Obsolete("Design-time only", error: true)]
	public AccountViewModel()
		: this(new ZcashAccount(new Zip32HDWallet(Bip39Mnemonic.Create(32), ZcashNetwork.TestNet)), (decimal)Random.Shared.Next(0, 10000) / 100)
	{
	}

	public AccountViewModel(ZcashAccount account, decimal balance)
	{
		this.SwitchToCommand = ReactiveCommand.Create(() => { });
		this.DeleteCommand = ReactiveCommand.Create(() => { });

		this.LinkProperty(nameof(this.AreKeysRevealed), nameof(this.RevealKeysCommandCaption));

		this.Balance = account.Network.AsSecurity().Amount(balance); // TODO: hook this up to a live feed so it updates as the wallet syncs.
		this.FullViewingKey = account.FullViewing?.UnifiedKey.TextEncoding;
		this.IncomingViewingKey = account.IncomingViewing.UnifiedKey.TextEncoding;
	}

	public string Name
	{
		get => this.name;
		set => this.RaiseAndSetIfChanged(ref this.name, value);
	}

	public bool IsIndexVisible => this.Index is not null;

	public string IndexCaption => "Account index: ";

	public required uint? Index { get; init; }

	public SecurityAmount Balance
	{
		get => this.balance;
		set => this.RaiseAndSetIfChanged(ref this.balance, value);
	}

	public bool AreKeysRevealed
	{
		get => this.areKeysRevealed;
		set => this.RaiseAndSetIfChanged(ref this.areKeysRevealed, value);
	}

	public string FullViewingKeyCaption => "Full viewing key";

	public string? FullViewingKey { get; }

	public string IncomingViewingKeyCaption => "Incoming viewing key";

	public string IncomingViewingKey { get; }

	public ReactiveCommand<Unit, Unit> SwitchToCommand { get; }

	public string SwitchToCommandCaption => "Switch to";

	public ReactiveCommand<Unit, Unit> DeleteCommand { get; }

	public string DeleteCommandCaption => "Delete";

	public string RevealKeysCommandCaption => "Reveal keys";
}
