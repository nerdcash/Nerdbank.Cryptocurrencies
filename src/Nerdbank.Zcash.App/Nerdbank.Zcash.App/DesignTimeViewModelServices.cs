// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Nerdbank.Cryptocurrencies;

namespace Nerdbank.Zcash.App;

internal class DesignTimeViewModelServices : IViewModelServices
{
	private Account? selectedAccount;

	internal DesignTimeViewModelServices(bool empty = false)
	{
		if (!empty)
		{
			// Populate accounts.
			Bip39Mnemonic mnemonic = Bip39Mnemonic.Create(Zip32HDWallet.MinimumEntropyLengthInBits);
			HDWallet zec = new(new(mnemonic, ZcashNetwork.MainNet));
			HDWallet taz = new(new(mnemonic, ZcashNetwork.TestNet));

			Account mainAccount = new(new ZcashAccount(taz.Zip32, 0), taz) { Name = "Main" };
			Account savingsAccount = new(new ZcashAccount(taz.Zip32, 1), taz) { Name = "Savings" };
			Account realAccount = new(new ZcashAccount(zec.Zip32, 0), zec) { Name = "Real ZEC" };

			this.Wallet.Add(mainAccount);
			this.Wallet.Add(savingsAccount);
			this.Wallet.Add(realAccount);

			this.SelectedAccount = mainAccount;

			// Populate address book.
			this.ContactManager.Contacts.Add(new Contact { Name = "Andrew Arnott", ReceivingAddress = ZcashAddress.Decode("t1a7w3qM23i4ajQcbX5wd6oH4zTY8Bry5vF") });
			this.ContactManager.Contacts.Add(new Contact { Name = "Jason Arnott", ReceivingAddress = ZcashAddress.Decode("u17kydrnuh9k8dqtud9qugel5ym835xqg8jk5czy2qcxea0zucru7d9w0c9hcq43898l2d993taaqh6vr0u6yskjnn582vyvu8qqk6qyme0z2vfgcclxatca7cx2f45v2n9zfd7hmkwlrw0wt38z9ua2yvgdnvppucyf2cfsxwlyfy339k") });
			this.ContactManager.Contacts.Add(new Contact { Name = "David Arnott" });
		}
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	public ZcashWallet Wallet { get; } = new();

	public Account? SelectedAccount
	{
		get => this.selectedAccount ??= this.Wallet.First();
		set => this.selectedAccount = value;
	}

	public HDWallet? SelectedHDWallet => this.SelectedAccount?.MemberOf;

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

	protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	private class DesignTimeContactManager : IContactManager
	{
		public ObservableCollection<Contact> Contacts { get; } = new();
	}
}
