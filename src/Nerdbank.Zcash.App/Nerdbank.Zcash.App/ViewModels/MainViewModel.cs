// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Cryptocurrencies;

namespace Nerdbank.Zcash.App.ViewModels;

public class MainViewModel : ViewModelBase
{
	public MainViewModel()
	{
		this.StartNewWalletCommand = ReactiveCommand.Create(this.CreateNewAccount);
		this.ImportWalletCommand = ReactiveCommand.Create(() => { });
	}

	public string Greeting => Strings.AppGreeting;

	public string StartNewWalletButtonText => Strings.StartNewWalletButtonText;

	public string StartNewWalletExplanation => Strings.StartNewWalletExplanation;

	public string ImportWalletButtonText => Strings.ImportWalletButtonText;

	public string ImportWalletExplanation => Strings.ImportWalletExplanation;

	public ReactiveCommand<Unit, Unit> StartNewWalletCommand { get; }

	public ReactiveCommand<Unit, Unit> ImportWalletCommand { get; }

	private void CreateNewAccount()
	{
		Bip39Mnemonic mnemonic = Bip39Mnemonic.Create(128);
		Zip32HDWallet zip32 = new(mnemonic);
		ZcashAccount account = new(zip32);
	}
}
