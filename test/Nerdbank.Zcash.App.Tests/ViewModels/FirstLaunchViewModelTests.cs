// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Linq;

namespace ViewModels;

public class FirstLaunchViewModelTests : ViewModelTestBase
{
	private readonly ITestOutputHelper logger;
	private FirstLaunchViewModel viewModel;

	public FirstLaunchViewModelTests(ITestOutputHelper logger)
	{
		this.viewModel = new(this.MainViewModel);
		this.logger = logger;
	}

	[Fact]
	public async Task CreateNewWallet()
	{
		await this.viewModel.StartNewWalletCommand.Execute().FirstAsync();

		Account mainNetAccount = Assert.Single(this.MainViewModel.Wallet.Accounts, a => a.Network == ZcashNetwork.MainNet);
		Assert.False(string.IsNullOrEmpty(mainNetAccount.Name));
		this.logger.WriteLine(mainNetAccount.Name);

		Account testNetAccount = Assert.Single(this.MainViewModel.Wallet.Accounts, a => a.Network == ZcashNetwork.TestNet);
		Assert.False(string.IsNullOrEmpty(testNetAccount.Name));
		this.logger.WriteLine(testNetAccount.Name);

		Assert.True(this.MainViewModel.Wallet.TryGetHDWallet(mainNetAccount, out HDWallet? wallet));
		Assert.False(string.IsNullOrEmpty(wallet.Name));
	}
}
