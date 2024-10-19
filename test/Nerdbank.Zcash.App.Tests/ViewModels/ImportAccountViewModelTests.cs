// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Linq;

namespace ViewModels;

public class ImportAccountViewModelTests : ViewModelTestBase
{
	private const string ValidSeedPhrase = "weapon solid program critic you long skill foot damp kingdom west history car crunch park increase excite hidden bless spot matter razor memory garbage";
	private const string OneWordPassword = "OneWord";
	private const string AnotherWordPassword = "AnotherWord";

	private readonly ITestOutputHelper logger;

	private ImportAccountViewModel viewModel;

	public ImportAccountViewModelTests(ITestOutputHelper logger)
	{
		this.logger = logger;
		this.viewModel = new(this.MainViewModel);
	}

	[Theory, PairwiseData]
	public async Task ValidSeedPhrase_NoPassword(bool isTestNet)
	{
		this.viewModel.Key = ValidSeedPhrase;
		Assert.True(this.viewModel.IsSeed);
		Assert.True(this.viewModel.IsPasswordVisible);
		Assert.Equal(string.Empty, this.viewModel.SeedPassword);
		Assert.False(this.viewModel.IsTestNet);
		this.viewModel.IsTestNet = isTestNet;

		Assert.True(await this.viewModel.ImportCommand.CanExecute.FirstAsync());
		Account? account = await this.viewModel.ImportCommand.Execute().FirstAsync();

		Assert.Equal(ValidSeedPhrase, account?.ZcashAccount.HDDerivation?.Wallet.Mnemonic?.SeedPhrase);
		Assert.Equal(string.Empty, account?.ZcashAccount.HDDerivation?.Wallet.Mnemonic?.Password.ToString());
		Assert.Equal<uint?>(0, account?.ZcashAccount.HDDerivation?.AccountIndex);
		Assert.Equal(isTestNet ? ZcashNetwork.TestNet : ZcashNetwork.MainNet, account?.Network);
	}

	[UIFact]
	public async Task ValidSeedPhrase_ExplicitPassword()
	{
		this.viewModel.Key = ValidSeedPhrase;
		this.viewModel.SeedPassword = OneWordPassword;
		Assert.True(this.viewModel.IsSeed);
		Assert.True(this.viewModel.IsPasswordVisible);

		Assert.True(await this.viewModel.ImportCommand.CanExecute.FirstAsync());
		Account? account = await this.viewModel.ImportCommand.Execute().FirstAsync();

		Assert.Equal(ValidSeedPhrase, account?.ZcashAccount.HDDerivation?.Wallet.Mnemonic?.SeedPhrase);
		Assert.Equal(OneWordPassword, account?.ZcashAccount.HDDerivation?.Wallet.Mnemonic?.Password.ToString());
		Assert.Equal<uint?>(0, account?.ZcashAccount.HDDerivation?.AccountIndex);
	}

	[UIFact]
	public async Task ValidSeedPhrase_ImplicitPassword()
	{
		this.viewModel.Key = $"{ValidSeedPhrase} {OneWordPassword}";
		Assert.True(this.viewModel.IsSeed);
		Assert.False(this.viewModel.IsPasswordVisible);

		Assert.True(await this.viewModel.ImportCommand.CanExecute.FirstAsync());
		Account? account = await this.viewModel.ImportCommand.Execute().FirstAsync();

		Assert.Equal(ValidSeedPhrase, account?.ZcashAccount.HDDerivation?.Wallet.Mnemonic?.SeedPhrase);
		Assert.Equal(OneWordPassword, account?.ZcashAccount.HDDerivation?.Wallet.Mnemonic?.Password.ToString());
		Assert.Equal<uint?>(0, account?.ZcashAccount.HDDerivation?.AccountIndex);
	}

	/// <summary>
	/// Simulates the user entering a seed phrase with an explicit password,
	/// but then changing the seed phrase to include the password in the seed phrase
	/// such that the password box disappears, while their explicit password was specified.
	/// </summary>
	[UIFact]
	public async Task ValidSeedPhrase_ImplicitPassword_WithIgnoredExplicitPassword()
	{
		// Enter regular seed phrase with explicit password.
		this.viewModel.Key = ValidSeedPhrase;
		Assert.True(this.viewModel.IsSeed);
		Assert.True(this.viewModel.IsPasswordVisible);
		this.viewModel.SeedPassword = OneWordPassword;

		// Now change the seed phrase to include the password in the seed phrase.
		this.viewModel.Key = $"{ValidSeedPhrase} {AnotherWordPassword}";
		Assert.True(this.viewModel.IsPasswordVisible);
		Assert.False(await this.viewModel.ImportCommand.CanExecute.FirstAsync());
		this.viewModel.SeedPassword = string.Empty;
		Assert.False(this.viewModel.IsPasswordVisible);
		Assert.True(await this.viewModel.ImportCommand.CanExecute.FirstAsync());

		Assert.True(await this.viewModel.ImportCommand.CanExecute.FirstAsync());
		Account? account = await this.viewModel.ImportCommand.Execute().FirstAsync();

		Assert.Equal(ValidSeedPhrase, account?.ZcashAccount.HDDerivation?.Wallet.Mnemonic?.SeedPhrase);
		Assert.Equal(AnotherWordPassword, account?.ZcashAccount.HDDerivation?.Wallet.Mnemonic?.Password.ToString());
		Assert.Equal<uint?>(0, account?.ZcashAccount.HDDerivation?.AccountIndex);
	}

	[UIFact]
	public async Task PrivateKey_Transparent()
	{
		Bip39Mnemonic mnemonic = Bip39Mnemonic.Parse(ValidSeedPhrase);
		Zip32HDWallet hdwallet = new(mnemonic, ZcashNetwork.TestNet);
		Zip32HDWallet.Transparent.ExtendedSpendingKey transparentAccount = hdwallet.CreateTransparentAccount();
		this.logger.WriteLine(transparentAccount.TextEncoding);

		this.viewModel.Key = transparentAccount.TextEncoding;
		Assert.False(this.viewModel.IsSeed);
		Assert.Equal(string.Empty, this.viewModel.SeedPassword);
		Assert.False(this.viewModel.IsPasswordVisible);

		Assert.True(await this.viewModel.ImportCommand.CanExecute.FirstAsync());
		Account? account = await this.viewModel.ImportCommand.Execute().FirstAsync();

		Assert.Equal(transparentAccount, account?.ZcashAccount.Spending?.Transparent);
		Assert.Null(account?.ZcashAccount.HDDerivation);
	}

	[UIFact]
	public void PasswordHasSpacesWarning()
	{
		this.viewModel.Key = ValidSeedPhrase;
		this.viewModel.SeedPassword = $" {OneWordPassword}";
		Assert.True(this.viewModel.SeedPasswordHasWhitespace);
		this.viewModel.SeedPassword = $"{OneWordPassword}\t";
		Assert.True(this.viewModel.SeedPasswordHasWhitespace);
	}
}
