// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Avalonia.Media.Imaging;

namespace Nerdbank.Zcash.App.ViewModels;

public class FirstLaunchViewModel : ViewModelBase, IHasTitle
{
	private readonly IViewModelServices viewModelServices;

	[Obsolete("For design-time use only.", error: true)]
	public FirstLaunchViewModel()
		: this(new DesignTimeViewModelServices(empty: true))
	{
	}

	public FirstLaunchViewModel(IViewModelServices viewModelServices)
	{
		this.StartNewWalletCommand = ReactiveCommand.Create(this.CreateNewAccount);
		this.StartNewWalletAdvancedCommand = ReactiveCommand.Create(this.CreateNewAccountAdvanced);
		this.ImportWalletCommand = ReactiveCommand.Create(this.ImportWallet);
		this.ViewLicenseCommand = ReactiveCommand.Create(this.ViewLicense);
		this.viewModelServices = viewModelServices;
	}

	public string Title => "Welcome";

	public string Greeting => Strings.AppGreeting;

	public Bitmap Logo => Resources.ZcashLogo;

	public string StartNewWalletButtonText => Strings.StartNewWalletButtonText;

	public string StartNewWalletExplanation => Strings.StartNewWalletExplanation;

	public string ImportWalletButtonText => Strings.ImportWalletButtonText;

	public string ImportWalletExplanation => Strings.ImportWalletExplanation;

	public string LicenseAcceptance => "Use of this application expresses agreement to its license terms. Absolutely no warranty is provided.";

	public string ViewLicenseCommandCaption => "View license";

	public ReactiveCommand<Unit, Unit> ViewLicenseCommand { get; }

	public ReactiveCommand<Unit, Unit> StartNewWalletCommand { get; }

	public string StartNewWalletAdvancedCommandCaption => Strings.StartNewWalletAdvancedCommandCaption;

	public ReactiveCommand<Unit, CreateNewWalletViewModel> StartNewWalletAdvancedCommand { get; }

	public ReactiveCommand<Unit, ImportAccountViewModel> ImportWalletCommand { get; }

	private void CreateNewAccount()
	{
		Bip39Mnemonic mnemonic = Bip39Mnemonic.Create(Zip32HDWallet.MinimumEntropyLengthInBits);

		this.NewAccount(mnemonic, ZcashNetwork.MainNet);
		this.NewAccount(mnemonic, ZcashNetwork.TestNet);

		this.viewModelServices.ReplaceViewStack(new HomeScreenViewModel(this.viewModelServices));
	}

	private void NewAccount(Bip39Mnemonic mnemonic, ZcashNetwork network)
	{
		Zip32HDWallet zip32 = new(mnemonic, network);
		Account accountModel = this.viewModelServices.Wallet.Add(new ZcashAccount(zip32));
		accountModel.Name = Strings.FormatDefaultNameForFirstAccount(network.AsSecurity().TickerSymbol);
		Assumes.True(this.viewModelServices.Wallet.TryGetHDWallet(accountModel, out HDWallet? wallet));
		wallet.Name = Strings.FormatDefaultNameForFirstHDWallet(zip32.Network);
	}

	private CreateNewWalletViewModel CreateNewAccountAdvanced()
	{
		CreateNewWalletViewModel newAccountViewModel = new(this.viewModelServices);
		newAccountViewModel.CreateAccountCommand.Subscribe(account =>
		{
			this.viewModelServices.ReplaceViewStack(new HomeScreenViewModel(this.viewModelServices));
		});
		return this.viewModelServices.NavigateTo(newAccountViewModel);
	}

	private ImportAccountViewModel ImportWallet()
	{
		ImportAccountViewModel importAccountViewModel = new(this.viewModelServices);
		importAccountViewModel.ImportCommand.Subscribe(account =>
		{
			if (account is not null)
			{
				// The user imported the wallet to begin with, so they evidently have a copy somewhere else.
				if (this.viewModelServices.Wallet.TryGetMnemonic(account, out ZcashMnemonic? mnemonic))
				{
					mnemonic.IsBackedUp = true;
				}

				this.viewModelServices.ReplaceViewStack(new HomeScreenViewModel(this.viewModelServices));
			}
		});
		return this.viewModelServices.NavigateTo(importAccountViewModel);
	}

	private void ViewLicense()
	{
		this.viewModelServices.NavigateTo(new AboutViewModel(this.viewModelServices));
	}
}
