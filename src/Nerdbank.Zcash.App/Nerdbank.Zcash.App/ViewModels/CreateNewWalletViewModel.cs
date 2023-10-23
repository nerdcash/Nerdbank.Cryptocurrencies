// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Cryptocurrencies;

namespace Nerdbank.Zcash.App.ViewModels;

public class CreateNewWalletViewModel : ViewModelBase, IHasTitle
{
	private readonly IViewModelServices viewModelServices;
	private string password = string.Empty;
	private bool isTestNet;

	[Obsolete("For design-time use only.", error: true)]
	public CreateNewWalletViewModel()
		: this(new DesignTimeViewModelServices())
	{
	}

	public CreateNewWalletViewModel(IViewModelServices viewModelServices)
	{
		this.viewModelServices = viewModelServices;

		this.CreateAccountCommand = ReactiveCommand.Create(this.CreateNewAccount);
	}

	public string Title => "Create new wallet";

	public string PasswordCaption => "Password";

	public string Password
	{
		get => this.password;
		set => this.RaiseAndSetIfChanged(ref this.password, value);
	}

	public string PasswordExplanation => "A password is optional. If specified, it is effectively an extra word in your seed phrase that is required to restore access to your funds on another device or wallet app. A password can be anything, but a single word is strongly recommended to allow restoring your wallet into other apps that permit only a one-word password.\n\nThis password is not required on every launch of this app.";

	public bool IsTestNet
	{
		get => this.isTestNet;
		set => this.RaiseAndSetIfChanged(ref this.isTestNet, value);
	}

	public string IsTestNetCaption => "This is a testnet account";

	public string CreateAccountButtonText => "Create account";

	public ReactiveCommand<Unit, Unit> CreateAccountCommand { get; }

	private void CreateNewAccount()
	{
		// ZIP-32 and the Zcash threat modeling requires (at least) 256-bit seeds (https://discord.com/channels/809218587167293450/972649509651906711/1165400226232803378).
		Bip39Mnemonic mnemonic = Bip39Mnemonic.Create(Zip32HDWallet.MinimumEntropyLengthInBits, this.Password);
		Zip32HDWallet zip32 = new(mnemonic, this.IsTestNet ? ZcashNetwork.TestNet : ZcashNetwork.MainNet);
		this.viewModelServices.Wallet.Add(new ZcashAccount(zip32, 0));
		this.viewModelServices.ReplaceViewStack(new HomeScreenViewModel((IViewModelServicesWithSelectedAccount)this.viewModelServices));
	}
}
