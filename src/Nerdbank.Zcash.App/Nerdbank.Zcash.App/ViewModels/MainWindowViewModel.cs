// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using DynamicData.Binding;

namespace Nerdbank.Zcash.App.ViewModels;

public class MainWindowViewModel : MainViewModel
{
	private static readonly string AppTitle = Strings.AppTitle;

	public MainWindowViewModel()
	{
		this.LinkProperty(nameof(this.Content), nameof(this.Title));

		IObservable<bool> accountSelected = this.WhenChanged(vm => vm.SelectedAccount, (vm, a) => a is not null);
		IObservable<bool> hdWalletSelected = this.WhenChanged(vm => vm.SelectedHDWallet, (vm, w) => w is not null);
		IObservable<bool> nonEmptyWallet = this.ObservableForProperty(vm => vm.Wallet.IsEmpty, w => w);

		this.AboutCommand = ReactiveCommand.Create(() => this.NavigateTo(new AboutViewModel()));
		this.SettingsCommand = ReactiveCommand.Create(() => this.NavigateTo(new SettingsViewModel(this)));
		this.AddressBookCommand = ReactiveCommand.Create(() => this.NavigateTo(new AddressBookViewModel(this)));
		this.AddressCheckCommand = ReactiveCommand.Create(() => this.NavigateTo(new MatchAddressViewModel(this)));
		this.HomeCommand = ReactiveCommand.Create(() => this.ReplaceViewStack(new HomeScreenViewModel(this)));
		this.AccountsListCommand = ReactiveCommand.Create(() => this.NavigateTo(new AccountsViewModel(this)), nonEmptyWallet);
		this.AccountBalanceCommand = ReactiveCommand.Create(() => this.NavigateTo(new BalanceViewModel(this)), accountSelected);
		this.TransactionHistoryCommand = ReactiveCommand.Create(() => this.NavigateTo(new HistoryViewModel(this)), accountSelected);
		this.SendCommand = ReactiveCommand.Create(() => this.NavigateTo(new SendingViewModel(this)), accountSelected);
		this.ReceiveCommand = ReactiveCommand.Create(() => this.NavigateTo(new ReceivingIntentSelectorViewModel(this)), accountSelected);
		this.BackupCommand = ReactiveCommand.Create(() => this.NavigateTo(new BackupViewModel(this, this.SelectedHDWallet)), nonEmptyWallet);
	}

	public string Title => this.Content is IHasTitle titledViewModel ? $"{titledViewModel.Title} - {AppTitle}" : AppTitle;

	public ReactiveCommand<Unit, Unit> AboutCommand { get; }

	public ReactiveCommand<Unit, Unit> HomeCommand { get; }

	public ReactiveCommand<Unit, Unit> AddressBookCommand { get; }

	public ReactiveCommand<Unit, Unit> SettingsCommand { get; }

	public ReactiveCommand<Unit, Unit> AddressCheckCommand { get; }

	public ReactiveCommand<Unit, Unit> AccountsListCommand { get; }

	public ReactiveCommand<Unit, Unit> AccountBalanceCommand { get; }

	public ReactiveCommand<Unit, Unit> TransactionHistoryCommand { get; }

	public ReactiveCommand<Unit, Unit> SendCommand { get; }

	public ReactiveCommand<Unit, Unit> ReceiveCommand { get; }

	public ReactiveCommand<Unit, Unit> BackupCommand { get; }

	public void NewAccount()
	{
		if (this.SelectedHDWallet is { } hdWallet)
		{
			ZcashAccount newAccount = hdWallet.AddAccount(this.SelectedHDWallet.MaxAccountIndex + 1);
			this.SelectedAccount = newAccount;
		}
	}
}
