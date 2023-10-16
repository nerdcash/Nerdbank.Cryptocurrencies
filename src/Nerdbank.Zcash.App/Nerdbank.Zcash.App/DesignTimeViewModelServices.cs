// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using Nerdbank.Cryptocurrencies;

namespace Nerdbank.Zcash.App;

internal class DesignTimeViewModelServices : IViewModelServicesWithWallet
{
	private ZcashAccount? selectedAccount;

	public ZcashWallet? Wallet { get; set; } = new()
	{
		Accounts =
		{
			[0] = new ZcashAccount(new Zip32HDWallet(Bip39Mnemonic.Create(128), ZcashNetwork.TestNet)),
		},
	};

	public ZcashAccount SelectedAccount
	{
		get => this.selectedAccount ??= this.Wallet!.Accounts.First().Value;
		set => this.selectedAccount = value;
	}

	public IContactManager ContactManager { get; } = new DesignTimeContactManager();

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

	private class DesignTimeContactManager : IContactManager
	{
		public ObservableCollection<Contact> Contacts { get; } = new();
	}
}
