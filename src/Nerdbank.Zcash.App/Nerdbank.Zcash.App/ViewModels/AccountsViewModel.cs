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

	private async Task<Account?> NewAccountAsync(ZcashNetwork network, CancellationToken cancellationToken)
	{
		ZcashWallet wallet = this.viewModelServices.Wallet;

		// Prefer the first HD wallet the user entered.
		HDWallet? hd = wallet.HDWallets.FirstOrDefault();
		if (hd is null)
		{
			hd = new HDWallet(Bip39Mnemonic.Create(Zip32HDWallet.MinimumEntropyLengthInBits))
			{
				Name = Strings.DefaultNameForFirstHDWallet,
			};
			wallet.Add(hd);
		}

		using ManagedLightWalletClient client = await ManagedLightWalletClient.CreateAsync(this.viewModelServices.Settings.GetLightServerUrl(network), cancellationToken);
		ulong birthdayHeight = await client.GetLatestBlockHeightAsync(cancellationToken);

		uint index = wallet.GetMaxAccountIndex(hd, network) is uint idx ? idx + 1 : 0;
		Account account = new Account(new ZcashAccount(hd.GetZip32HDWalletByNetwork(network), index))
		{
			Name = $"Account {index} ({network.AsSecurity().TickerSymbol})",
			ZcashAccount =
			{
				BirthdayHeight = birthdayHeight,
			},
		};
		wallet.Add(account);
		return account;
	}
}
