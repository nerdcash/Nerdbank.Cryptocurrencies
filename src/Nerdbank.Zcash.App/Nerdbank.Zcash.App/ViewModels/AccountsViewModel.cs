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
	}

	public string Title => "Accounts";

	public ObservableCollection<AccountViewModel> Accounts { get; } = new();

	public bool GroupAccountsByHDWallets => this.groupAccountsByHDWallets.Value;

	public string AccountNameColumnHeader => "Name";

	public string AccountBalanceColumnHeader => $"Balance";

	public string AccountIndexColumnHeader => "Index";

	public ReactiveCommand<Unit, Unit> NewAccountCommand { get; }

	public string NewAccountCommandCaption => "New account";

	public ReactiveCommand<Unit, ImportAccountViewModel> ImportAccountCommand { get; }

	public string ImportAccountCommandCaption => "Import account";

	public ImportAccountViewModel ImportAccount()
	{
		ImportAccountViewModel importAccount = new(this.viewModelServices);
		importAccount.ImportCommand.Subscribe(_ => this.viewModelServices.NavigateBack(importAccount));
		return this.viewModelServices.NavigateTo(importAccount);
	}

	public void NewAccount()
	{
		////Verify.Operation(this.viewModelServices.SelectedHDWallet is not null, "No HD wallet selected.");
		////this.viewModelServices.SelectedHDWallet.AddAccount(this.viewModelServices.SelectedHDWallet.MaxAccountIndex + 1);
	}
}
