// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Avalonia.Media.Imaging;
using Nerdbank.Cryptocurrencies;

namespace Nerdbank.Zcash.App.ViewModels;

public class FirstLaunchViewModel : ViewModelBase
{
	private readonly IViewModelServices viewModelServices;

	[Obsolete("For design-time use only.", error: true)]
	public FirstLaunchViewModel()
		: this(new DesignTimeViewModelServices())
	{
	}

	public FirstLaunchViewModel(IViewModelServices viewModelServices)
	{
		this.StartNewWalletCommand = ReactiveCommand.Create(this.CreateNewAccount);
		this.StartNewWalletAdvancedCommand = ReactiveCommand.Create(this.CreateNewAccountAdvanced);
		this.ImportWalletCommand = ReactiveCommand.Create(this.ImportWallet);
		this.viewModelServices = viewModelServices;
	}

	public string Greeting => Strings.AppGreeting;

	public Bitmap Logo => Resources.ZcashLogo;

	public string StartNewWalletButtonText => Strings.StartNewWalletButtonText;

	public string StartNewWalletExplanation => Strings.StartNewWalletExplanation;

	public string ImportWalletButtonText => Strings.ImportWalletButtonText;

	public string ImportWalletExplanation => Strings.ImportWalletExplanation;

	public ReactiveCommand<Unit, Unit> StartNewWalletCommand { get; }

	public string StartNewWalletAdvancedCommandCaption => Strings.StartNewWalletAdvancedCommandCaption;

	public ReactiveCommand<Unit, Unit> StartNewWalletAdvancedCommand { get; }

	public ReactiveCommand<Unit, Unit> ImportWalletCommand { get; }

	private void CreateNewAccount()
	{
		Bip39Mnemonic mnemonic = Bip39Mnemonic.Create(128);
		Zip32HDWallet zip32 = new(mnemonic);
		uint index = 0;
		ZcashAccount account = new(zip32, index);

		this.viewModelServices.Wallet = new ZcashWallet()
		{
			Mnemonic = mnemonic,
			Accounts = { [index] = account },
		};
		this.viewModelServices.ReplaceViewStack(new HomeScreenViewModel((IViewModelServicesWithWallet)this.viewModelServices));
	}

	private void CreateNewAccountAdvanced()
	{
		this.viewModelServices.NavigateTo(new CreateNewWalletViewModel(this.viewModelServices));
	}

	private void ImportWallet()
	{
		ImportAccountViewModel importAccountViewModel = new();
		importAccountViewModel.ImportCommand.Subscribe(account =>
		{
			this.viewModelServices.Wallet = new ZcashWallet()
			{
				//Accounts = { [account.Index] = account },
			};

			// The user imported the wallet to begin with, so they evidently have a copy somewhere else.
			this.viewModelServices.Wallet.IsSeedPhraseBackedUp = true;

			this.viewModelServices.ReplaceViewStack(new HomeScreenViewModel((IViewModelServicesWithWallet)this.viewModelServices));
		});
		this.viewModelServices.NavigateTo(importAccountViewModel);
	}
}
