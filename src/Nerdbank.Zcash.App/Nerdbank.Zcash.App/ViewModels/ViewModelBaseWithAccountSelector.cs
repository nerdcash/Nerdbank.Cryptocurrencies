// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Cryptocurrencies.Exchanges;

namespace Nerdbank.Zcash.App.ViewModels;

public abstract class ViewModelBaseWithAccountSelector : ViewModelBase
{
	protected static readonly Security UnknownSecurity = new(string.Empty);
	private bool accountPickerIsVisible;
	private Account? selectedAccount;

	public ViewModelBaseWithAccountSelector(IViewModelServices viewModelServices)
	{
		this.ViewModelServices = viewModelServices;

		this.Accounts = viewModelServices.Wallet.ToList();
		this.accountPickerIsVisible = this.Accounts.Count > 1;

		this.selectedAccount = viewModelServices.MostRecentlyUsedAccount ?? viewModelServices.Wallet.FirstOrDefault();

		this.LinkProperty(nameof(this.SelectedAccount), nameof(this.SelectedSecurity));
	}

	public IViewModelServices ViewModelServices { get; }

	public IReadOnlyList<Account> Accounts { get; }

	public Account? SelectedAccount
	{
		get => this.selectedAccount;
		set
		{
			if (this.selectedAccount != value)
			{
				this.ViewModelServices.MostRecentlyUsedAccount = value;
				this.RaiseAndSetIfChanged(ref this.selectedAccount, value);
			}
		}
	}

	public bool AccountPickerIsVisible => this.accountPickerIsVisible;

	public Security SelectedSecurity => this.SelectedAccount?.Network.AsSecurity() ?? UnknownSecurity;
}
