// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Reactive.Linq;
using DynamicData.Binding;

namespace Nerdbank.Zcash.App.ViewModels;

public class AccountsViewModel : ViewModelBase, IHasTitle
{
	private readonly IViewModelServices viewModelServices;
	private readonly ObservableAsPropertyHelper<bool> groupAccountsByHDWallets;
	private bool isWatchOnlyColumnVisible;

	[Obsolete("Design-time only", error: true)]
	public AccountsViewModel()
		: this(new DesignTimeViewModelServices())
	{
	}

	public AccountsViewModel(IViewModelServices viewModelServices)
	{
		this.viewModelServices = viewModelServices;

		this.NewAccountCommand = ReactiveCommand.Create(this.NewAccount);
		this.ImportAccountCommand = ReactiveCommand.Create(this.ImportAccount);

		WrapModels(this.viewModelServices.Wallet.Accounts, this.Accounts, (Account a) => new AccountViewModel(a, viewModelServices));

		bool ShouldGroupAccounts() => this.Accounts.Select(a => a.GroupName).Distinct().Skip(1).Any();
		this.groupAccountsByHDWallets = this.Accounts.ObserveCollectionChanges().Select(
			_ => ShouldGroupAccounts())
			.ToProperty(this, nameof(this.GroupAccountsByHDWallets), initialValue: ShouldGroupAccounts());

		bool UpdateIsWatchOnlyColumnVisibility() => this.IsWatchOnlyColumnVisible = this.Accounts.Any(a => a.IsWatchOnly);
		this.Accounts.NotifyOnCollectionElementMemberChanged<ObservableCollection<AccountViewModel>, AccountViewModel>(
			nameof(AccountViewModel.IsWatchOnly),
			_ => UpdateIsWatchOnlyColumnVisibility());
		UpdateIsWatchOnlyColumnVisibility();
	}

	public string Title => AccountsStrings.Title;

	public ObservableCollection<AccountViewModel> Accounts { get; } = new();

	public bool GroupAccountsByHDWallets => this.groupAccountsByHDWallets.Value;

	public string AccountNameColumnHeader => AccountsStrings.AccountNameColumnHeader;

	public string AccountBalanceColumnHeader => AccountsStrings.AccountBalanceColumnHeader;

	public string AccountIndexColumnHeader => AccountsStrings.AccountIndexColumnHeader;

	public string IsWatchOnlyColumnHeader => AccountsStrings.IsWatchOnlyColumnHeader;

	public bool IsWatchOnlyColumnVisible
	{
		get => this.isWatchOnlyColumnVisible;
		set => this.RaiseAndSetIfChanged(ref this.isWatchOnlyColumnVisible, value);
	}

	public ReactiveCommand<Unit, Unit> NewAccountCommand { get; }

	public string NewAccountCommandCaption => AccountsStrings.NewAccountCommandCaption;

	public ReactiveCommand<Unit, ImportAccountViewModel> ImportAccountCommand { get; }

	public string ImportAccountCommandCaption => AccountsStrings.ImportAccountCommandCaption;

	public ImportAccountViewModel ImportAccount()
	{
		ImportAccountViewModel importAccount = new(this.viewModelServices);
		importAccount.ImportCommand.Subscribe(_ => this.viewModelServices.NavigateBack(importAccount));
		return this.viewModelServices.NavigateTo(importAccount);
	}

	private void NewAccount()
	{
		this.viewModelServices.NavigateTo(new CreateNewAccountDetailsViewModel(this.viewModelServices));
	}
}
