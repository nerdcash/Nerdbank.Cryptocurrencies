// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;

namespace Nerdbank.Zcash.App.ViewModels;

public class CreateNewWalletViewModel : ViewModelBase, IHasTitle
{
	private readonly IViewModelServices viewModelServices;
	private readonly ObservableAsPropertyHelper<bool> passwordContainsWhitespace;
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

		IObservable<bool> containsWhitespace = this.WhenAnyValue(x => x.Password, x => x.Any(char.IsWhiteSpace));
		this.passwordContainsWhitespace = containsWhitespace.ToProperty(this, nameof(this.PasswordContainsWhitespace));

		this.CreateAccountCommand = ReactiveCommand.CreateFromTask(this.CreateNewAccountAsync);
		this.RemoveWhitespaceCommand = ReactiveCommand.Create(this.RemoveWhitespace, containsWhitespace);
	}

	public string Title => CreateNewWalletStrings.Title;

	public string Introduction => CreateNewWalletStrings.Introduction;

	public string PasswordCaption => CreateNewWalletStrings.PasswordCaption;

	public string Password
	{
		get => this.password;
		set => this.RaiseAndSetIfChanged(ref this.password, value);
	}

	public string PasswordExplanation => CreateNewWalletStrings.PasswordExplanation;

	public bool PasswordContainsWhitespace => this.passwordContainsWhitespace.Value;

	public string PasswordContainsWhitespaceWarning => CreateNewWalletStrings.PasswordContainsWhitespaceWarning;

	public ReactiveCommand<Unit, Unit> RemoveWhitespaceCommand { get; }

	public string RemoveWhitespaceCommandCaption => CreateNewWalletStrings.RemoveWhitespaceCommandCaption;

	public bool IsTestNet
	{
		get => this.isTestNet;
		set => this.RaiseAndSetIfChanged(ref this.isTestNet, value);
	}

	public string IsTestNetCaption => CreateNewWalletStrings.IsTestNetCaption;

	public string CreateAccountButtonText => CreateNewWalletStrings.CreateAccountButtonText;

	public ReactiveCommand<Unit, Account> CreateAccountCommand { get; }

	private async Task<Account> CreateNewAccountAsync()
	{
		// ZIP-32 and the Zcash threat modeling requires (at least) 256-bit seeds (https://discord.com/channels/809218587167293450/972649509651906711/1165400226232803378).
		Bip39Mnemonic mnemonic = Bip39Mnemonic.Create(Zip32HDWallet.MinimumEntropyLengthInBits, this.Password);
		Zip32HDWallet zip32 = new(mnemonic, this.IsTestNet ? ZcashNetwork.TestNet : ZcashNetwork.MainNet);
		ZcashAccount account = new(zip32, 0);

		account.BirthdayHeight = await AppUtilities.GetChainLengthAsync(this.viewModelServices, zip32.Network, CancellationToken.None);

		return this.viewModelServices.Wallet.Add(account);
	}

	private void RemoveWhitespace()
	{
		StringBuilder builder = new(this.Password);
		for (int i = builder.Length - 1; i >= 0; i--)
		{
			if (char.IsWhiteSpace(builder[i]))
			{
				builder.Remove(i, 1);
			}
		}

		this.Password = builder.ToString();
	}
}
