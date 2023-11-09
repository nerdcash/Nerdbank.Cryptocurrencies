// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Linq;

namespace ViewModels;

public class AccountsViewModelTests : ViewModelTestBase
{
	private AccountsViewModel viewModel = null!; // set in InitializeAsync

	public AccountsViewModelTests()
	{
	}

	public override async Task InitializeAsync()
	{
		await base.InitializeAsync();

		await this.InitializeWalletAsync();
		this.viewModel = new AccountsViewModel(this.MainViewModel);
	}

	[Theory]
	[PairwiseData]
	public async Task NewAccount_CreatesAccountImmediatelyWhenOnlyOneHDWallet(ZcashNetwork network)
	{
		HDWallet hd = Assert.Single(this.MainViewModel.Wallet.HDWallets, w => w.Zip32.Network == network);

		Account newAccount = await this.viewModel.NewAccountCommand.Execute(network).FirstAsync();
		Assert.NotNull(newAccount);
		Assert.Same(hd.Zip32, newAccount.ZcashAccount.HDDerivation?.Wallet);
		Assert.Equal<uint?>(1, newAccount.ZcashAccount.HDDerivation?.AccountIndex);
		Assert.Equal($"Account 1 ({network.AsSecurity().TickerSymbol})", newAccount.Name);

		Account newAccount2 = await this.viewModel.NewAccountCommand.Execute(network).FirstAsync();
		Assert.NotNull(newAccount2);
		Assert.Same(hd.Zip32, newAccount2.ZcashAccount.HDDerivation?.Wallet);
		Assert.Equal<uint?>(2, newAccount2.ZcashAccount.HDDerivation?.AccountIndex);
		Assert.Equal($"Account 2 ({network.AsSecurity().TickerSymbol})", newAccount2.Name);
	}

	[Fact]
	public async Task NewAccount_WithMultipleHDWallets()
	{
		ZcashNetwork network = ZcashNetwork.MainNet;
		HDWallet firstHD = Assert.Single(this.MainViewModel.Wallet.HDWallets, w => w.Zip32.Network == network);

		// Create a second HD wallet.
		this.MainViewModel.Wallet.Add(new ZcashAccount(new Zip32HDWallet(Bip39Mnemonic.Create(Zip32HDWallet.MinimumEntropyLengthInBits), network)));

		Account newAccount = await this.viewModel.NewAccountCommand.Execute(ZcashNetwork.MainNet).FirstAsync();
		Assert.Same(firstHD.Zip32, newAccount.ZcashAccount.HDDerivation?.Wallet);
	}
}
