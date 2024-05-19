// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ViewModels;

public class ViewModelBaseWithAccountSelectorTests : ViewModelTestBase
{
	[Fact]
	public void SelectedAccount_NoAccounts()
	{
		DerivedView view = new(this.MainViewModel);
		Assert.Null(view.SelectedAccount);
	}

	[Fact]
	public void SelectedSecurity_NoAccounts()
	{
		DerivedView view = new(this.MainViewModel);
		Assert.NotNull(view.SelectedSecurity);
		Assert.Equal(string.Empty, view.SelectedSecurity.TickerSymbol);
	}

	[Fact]
	public void SelectedAccount_DefaultsToAppSelection()
	{
		Bip39Mnemonic mnemonic = Bip39Mnemonic.Create(Zip32HDWallet.MinimumEntropyLengthInBits);
		Zip32HDWallet zip32 = new(mnemonic, ZcashNetwork.TestNet);
		Account mainAccount = new(new ZcashAccount(zip32, 0) { BirthdayHeight = 123456 }) { Name = "Main" };
		Account savingsAccount = new(new ZcashAccount(zip32, 1) { BirthdayHeight = 123456 }) { Name = "Savings" };
		this.MainViewModel.Wallet.Add(mainAccount);
		this.MainViewModel.Wallet.Add(savingsAccount);
		this.MainViewModel.MostRecentlyUsedAccount = savingsAccount;

		DerivedView view = new(this.MainViewModel);
		Assert.Same(savingsAccount, view.SelectedAccount);
	}

	private class DerivedView : ViewModelBaseWithAccountSelector
	{
		public DerivedView(IViewModelServices viewModelServices)
			: base(viewModelServices)
		{
		}
	}
}
