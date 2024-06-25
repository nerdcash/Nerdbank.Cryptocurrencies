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
		this.StartNewWalletCommand = ReactiveCommand.CreateFromTask(this.CreateNewAccountAsync);
		this.StartNewWalletAdvancedCommand = ReactiveCommand.Create(this.CreateNewAccountAdvanced);
		this.ImportWalletCommand = ReactiveCommand.Create(this.ImportWallet);
		this.ViewLicenseCommand = ReactiveCommand.Create(this.ViewLicense);
		this.ShowCapabilitiesCommand = ReactiveCommand.Create(() => viewModelServices.NavigateTo(new CapabilitiesViewModel()));
		this.viewModelServices = viewModelServices;
	}

	public string Title => FirstLaunchStrings.Title;

	public string Greeting => FirstLaunchStrings.AppGreeting;

	public Bitmap Logo => Resources.AppLogo;

	public string WalletForHeading => FirstLaunchStrings.WalletForHeading;

	public string StartNewWalletButtonText => FirstLaunchStrings.StartNewWalletButtonText;

	public string StartNewWalletExplanation => FirstLaunchStrings.StartNewWalletExplanation;

	public string ImportWalletButtonText => FirstLaunchStrings.ImportWalletButtonText;

	public string ImportWalletExplanation => FirstLaunchStrings.ImportWalletExplanation;

	public string LicenseAcceptance => FirstLaunchStrings.LicenseAcceptance;

	public string ViewLicenseCommandCaption => FirstLaunchStrings.ViewLicenseCommandCaption;

	public ReactiveCommand<Unit, Unit> ViewLicenseCommand { get; }

	public ReactiveCommand<Unit, Unit> StartNewWalletCommand { get; }

	public string ShowCapabilitiesCommandCaption => FirstLaunchStrings.FirstLaunchWalletCapabilitiesCommandCaption;

	public ReactiveCommand<Unit, CapabilitiesViewModel> ShowCapabilitiesCommand { get; }

	public string StartNewWalletAdvancedCommandCaption => FirstLaunchStrings.StartNewWalletAdvancedCommandCaption;

	public ReactiveCommand<Unit, CreateNewWalletViewModel> StartNewWalletAdvancedCommand { get; }

	public ReactiveCommand<Unit, ImportAccountViewModel> ImportWalletCommand { get; }

	private async Task CreateNewAccountAsync(CancellationToken cancellationToken)
	{
		Bip39Mnemonic mnemonic = Bip39Mnemonic.Create(Zip32HDWallet.MinimumEntropyLengthInBits);

		await this.NewAccountAsync(mnemonic, ZcashNetwork.MainNet, cancellationToken);
		await this.NewAccountAsync(mnemonic, ZcashNetwork.TestNet, cancellationToken);

		this.viewModelServices.ReplaceViewStack(new HomeScreenViewModel(this.viewModelServices));
	}

	private async Task NewAccountAsync(Bip39Mnemonic mnemonic, ZcashNetwork network, CancellationToken cancellationToken)
	{
		uint birthdayHeight = await AppUtilities.GetChainLengthAsync(this.viewModelServices, network, cancellationToken);

		Zip32HDWallet zip32 = new(mnemonic, network);
		Account accountModel = this.viewModelServices.Wallet.Add(new ZcashAccount(zip32) { BirthdayHeight = birthdayHeight });
		accountModel.Name = FirstLaunchStrings.FormatDefaultNameForFirstAccountWithTicker(network.AsSecurity().TickerSymbol);
		Assumes.True(this.viewModelServices.Wallet.TryGetHDWallet(accountModel, out HDWallet? wallet));
		wallet.Name = FirstLaunchStrings.DefaultNameForFirstHDWallet;
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
				if (this.viewModelServices.Wallet.TryGetHDWallet(account, out HDWallet? hd))
				{
					hd.IsBackedUp = true;
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
