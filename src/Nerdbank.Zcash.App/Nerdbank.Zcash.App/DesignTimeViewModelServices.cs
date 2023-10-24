// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using Avalonia.Controls;
using Nerdbank.Cryptocurrencies;

namespace Nerdbank.Zcash.App;

internal class DesignTimeViewModelServices : IViewModelServicesWithSelectedAccount
{
	private ZcashAccount? selectedAccount;

	internal DesignTimeViewModelServices()
	{
	}

	public ZcashWallet Wallet { get; } = new()
	{
		new ZcashAccount(new Zip32HDWallet(Bip39Mnemonic.Create(128), ZcashNetwork.TestNet)),
	};

	public ZcashAccount? SelectedAccount
	{
		get => this.selectedAccount ??= this.Wallet.First();
		set => this.selectedAccount = value;
	}

	ZcashAccount IViewModelServicesWithSelectedAccount.SelectedAccount
	{
		get => this.SelectedAccount ?? throw new InvalidOperationException();
		set => this.SelectedAccount = value;
	}

	public HDWallet? SelectedHDWallet => this.SelectedAccount is not null ? this.Wallet.GetHDWalletFor(this.SelectedAccount) : null;

	public IContactManager ContactManager { get; } = new DesignTimeContactManager();

	public TopLevel? TopLevel => null;

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
