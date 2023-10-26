// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Cryptocurrencies.Exchanges;

namespace Nerdbank.Zcash.App.ViewModels;

public abstract class ViewModelBaseWithAccountSelector : ViewModelBase
{
	protected static readonly Security UnknownSecurity = new(string.Empty);
	private bool accountPickerIsVisible;

	public ViewModelBaseWithAccountSelector(IViewModelServices viewModelServices)
	{
		this.ViewModelServices = viewModelServices;

		this.Accounts = viewModelServices.Wallet.AllAccounts.SelectMany(g => g).ToList();
		this.accountPickerIsVisible = this.Accounts.Count > 1;

		this.LinkProperty(nameof(this.SelectedAccount), nameof(this.SelectedSecurity));
	}

	public IViewModelServices ViewModelServices { get; }

	public IReadOnlyList<Account> Accounts { get; }

	public Account? SelectedAccount
	{
		get => this.ViewModelServices.SelectedAccount;
		set
		{
			if (this.ViewModelServices.SelectedAccount != value)
			{
				this.ViewModelServices.SelectedAccount = value;
				this.RaisePropertyChanged();
			}
		}
	}

	public bool AccountPickerIsVisible => this.accountPickerIsVisible;

	public Security SelectedSecurity => this.SelectedAccount?.Network.AsSecurity() ?? UnknownSecurity;
}
