// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;

namespace Nerdbank.Zcash.App.ViewModels;

public class AccountsViewModel : ViewModelBase, IHasTitle
{
	private readonly IViewModelServices viewModelServices;

	[Obsolete("Design-time only", error: true)]
	public AccountsViewModel()
		: this(new DesignTimeViewModelServices())
	{
		this.Accounts.Add(new AccountViewModel() { Name = "Spending", Index = 0 });
		this.Accounts.Add(new AccountViewModel() { Name = "Savings", Index = 1 });
	}

	public AccountsViewModel(IViewModelServices viewModelServices)
	{
		this.viewModelServices = viewModelServices;

		this.NewAccountCommand = ReactiveCommand.Create(() => { });
		this.ImportAccountCommand = ReactiveCommand.Create(() => { });
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
}
