// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Reactive.Linq;
using DynamicData;
using DynamicData.Binding;
using Nerdbank.Cryptocurrencies;

namespace Nerdbank.Zcash.App.ViewModels;

public abstract class ViewModelBaseWithAccountSelector : ViewModelBase
{
	protected static readonly Security UnknownSecurity = new(string.Empty);
	private readonly ReadOnlyObservableCollection<Account> accounts;
	private ObservableAsPropertyHelper<bool> accountPickerIsVisible;
	private ObservableAsPropertyHelper<SyncProgressData?> syncProgress;
	private Account? selectedAccount;

	public ViewModelBaseWithAccountSelector(IViewModelServices viewModelServices, bool showOnlyAccountsWithSpendKeys = false)
	{
		this.ViewModelServices = viewModelServices;

		SourceList<Account> sl = new(viewModelServices.Wallet.Accounts.AsObservableChangeSet());
		sl.Connect()
			.Sort(SortExpressionComparer<Account>.Ascending(a => a.Name))
			.Filter(a => !showOnlyAccountsWithSpendKeys || a.ZcashAccount.Spending is not null)
			.Bind(out this.accounts)
			.Subscribe();

		// The account picker should be visible as soon as the user has more than one account,
		// even if only one account is available for selection (due to account filtering).
		this.accountPickerIsVisible = sl.Connect().Select(_ => sl.Count > 1).ToProperty(this, nameof(this.AccountPickerIsVisible));

		this.selectedAccount = viewModelServices.MostRecentlyUsedAccount ?? viewModelServices.Wallet.Accounts.FirstOrDefault();

		this.syncProgress = this.WhenAnyValue(
			x => x.SelectedAccount,
			account => account is not null ? viewModelServices.App.WalletSyncManager?.GetSyncProgress(account.Network) : null)
			.ToProperty(this, nameof(this.SyncProgress));

		this.LinkProperty(nameof(this.SelectedAccount), nameof(this.SelectedSecurity));
	}

	public IViewModelServices ViewModelServices { get; }

	public ReadOnlyObservableCollection<Account> Accounts => this.accounts;

	public Account? SelectedAccount
	{
		get => this.selectedAccount;
		set
		{
			if (this.selectedAccount != value)
			{
				this.ViewModelServices.MostRecentlyUsedAccount = value;
				this.selectedAccount = value;
				this.RaisePropertyChanged();
				this.OnSelectedAccountChanged();
			}
		}
	}

	public SyncProgressData? SyncProgress => this.syncProgress.Value;

	public bool AccountPickerIsVisible => this.accountPickerIsVisible.Value;

	public Security SelectedSecurity => this.SelectedAccount?.Network.AsSecurity() ?? UnknownSecurity;

	protected virtual void OnSelectedAccountChanged()
	{
	}
}
