// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Cryptocurrencies;

namespace Nerdbank.Zcash.App.ViewModels;

public class CreateNewWalletViewModel : ViewModelBase
{
	private readonly IViewModelServices viewModelServices;
	private int entropyLength = 128;
	private string password = string.Empty;

	[Obsolete("For design-time use only.", error: true)]
	public CreateNewWalletViewModel()
		: this(new DesignTimeViewModelServices())
	{
	}

	public CreateNewWalletViewModel(IViewModelServices viewModelServices)
	{
		this.viewModelServices = viewModelServices;

		this.LinkProperty(nameof(this.EntropyLength), nameof(this.SeedPhraseWordCount));

		this.CreateAccountCommand = ReactiveCommand.Create(this.CreateNewAccount);
	}

	public string Title => "Create new wallet";

	public string EntropyLengthCaption => "Seed phrase length";

	public int EntropyLengthMin => 128;

	public int EntropyLengthMax => 256;

	public int EntropyStepSize => 32;

	public int EntropyLength
	{
		get => this.entropyLength;
		set => this.RaiseAndSetIfChanged(ref this.entropyLength, value);
	}

	public int SeedPhraseWordCount => this.EntropyLength / 32 * 3;

	public string PasswordCaption => "Password";

	public string Password
	{
		get => this.password;
		set => this.RaiseAndSetIfChanged(ref this.password, value);
	}

	public string PasswordExplanation => "A password is optional. If specified, it is effectively an extra word in your seed phrase that is required to restore access to your funds on another device or wallet app. A password can be anything, but a single word is strongly recommended to allow restoring your wallet into other apps that permit only a one-word password.";

	public string CreateAccountButtonText => "Create account";

	public ReactiveCommand<Unit, Unit> CreateAccountCommand { get; }

	private void CreateNewAccount()
	{
		Bip39Mnemonic mnemonic = Bip39Mnemonic.Create(this.EntropyLength, this.Password);
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
}
