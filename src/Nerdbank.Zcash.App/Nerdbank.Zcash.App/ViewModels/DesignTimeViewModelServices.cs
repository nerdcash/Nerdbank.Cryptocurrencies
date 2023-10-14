// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Cryptocurrencies;

namespace Nerdbank.Zcash.App.ViewModels;

internal class DesignTimeViewModelServices : IViewModelServicesWithWallet
{
	public ZcashWallet? Wallet { get; set; } = new()
	{
		Accounts =
		{
			[0] = new ZcashAccount(new Zip32HDWallet(Bip39Mnemonic.Create(128), ZcashNetwork.TestNet)),
		},
	};

	ZcashWallet IViewModelServicesWithWallet.Wallet => this.Wallet ?? throw new InvalidOperationException();

	public void NavigateBack(ViewModelBase? ifCurrentViewModel)
	{
	}

	public void NavigateTo(ViewModelBase viewModel)
	{
	}

	public void ReplaceViewStack(ViewModelBase viewModel)
	{
	}
}
