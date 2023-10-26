// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using DynamicData;
using Microsoft;
using Nerdbank.Cryptocurrencies;

namespace Nerdbank.Zcash.App.ViewModels;

public class AccountsViewModel : ViewModelBase, IHasTitle
{
	private readonly IViewModelServices viewModelServices;

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

		this.Accounts.AddRange(
			this.viewModelServices.Wallet.AllAccounts.SelectMany(a => a).Select(a => new AccountViewModel(a)));
	}

	public string Title => "Accounts";

	public ObservableCollection<AccountViewModel> Accounts { get; } = new();

	public string AccountNameColumnHeader => "Name";

	public string AccountBalanceColumnHeader => $"Balance";

	public string AccountIndexColumnHeader => "Index";

	public ReactiveCommand<Unit, Unit> NewAccountCommand { get; }

	public string NewAccountCommandCaption => "New account";

	public ReactiveCommand<Unit, Unit> ImportAccountCommand { get; }

	public string ImportAccountCommandCaption => "Import account";

	public void ImportAccount()
	{
		this.viewModelServices.NavigateTo(new ImportAccountViewModel(this.viewModelServices));
	}

	public void NewAccount()
	{
		Verify.Operation(this.viewModelServices.SelectedHDWallet is not null, "No HD wallet selected.");
		this.viewModelServices.SelectedHDWallet.AddAccount(this.viewModelServices.SelectedHDWallet.MaxAccountIndex + 1);
	}
}
