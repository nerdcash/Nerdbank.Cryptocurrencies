// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
		HDWallet taz = new(new(mnemonic, ZcashNetwork.TestNet));
		Account mainAccount = new(new ZcashAccount(taz.Zip32, 0), taz) { Name = "Main" };
		Account savingsAccount = new(new ZcashAccount(taz.Zip32, 1), taz) { Name = "Savings" };
		this.MainViewModel.Wallet.Add(mainAccount);
		this.MainViewModel.Wallet.Add(savingsAccount);
		this.MainViewModel.SelectedAccount = savingsAccount;

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
