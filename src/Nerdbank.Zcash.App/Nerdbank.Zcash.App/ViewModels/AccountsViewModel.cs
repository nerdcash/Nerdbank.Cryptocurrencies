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

		this.NewAccountCommand = ReactiveCommand.CreateFromTask<ZcashNetwork, Account?>(this.NewAccountAsync);
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

	public ReactiveCommand<ZcashNetwork, Account?> NewAccountCommand { get; }

	public string NewAccountMainNetCommandCaption => "New account";

	public string NewAccountTestNetCommandCaption => "New testnet account";

	public ReactiveCommand<Unit, ImportAccountViewModel> ImportAccountCommand { get; }

	public string ImportAccountCommandCaption => "Import account";

	public ImportAccountViewModel ImportAccount()
	{
		ImportAccountViewModel importAccount = new(this.viewModelServices);
		importAccount.ImportCommand.Subscribe(_ => this.viewModelServices.NavigateBack(importAccount));
		return this.viewModelServices.NavigateTo(importAccount);
	}

	public async Task<Account?> NewAccountAsync(ZcashNetwork network)
	{
		ZcashWallet wallet = this.viewModelServices.Wallet;
		HDWallet[] matchingHDWallets = wallet.HDWallets.Where(w => w.Zip32.Network == network).ToArray();
		if (matchingHDWallets.Length == 1)
		{
			HDWallet hd = matchingHDWallets[0];
			uint index = wallet.GetMaxAccountIndex(hd) + 1;
			Account account = new Account(new ZcashAccount(hd.Zip32, index))
			{
				Name = $"Account {index} ({network.AsSecurity().TickerSymbol})",
			};
			wallet.Add(account);
			return account;
		}

		return null;
	}
}
