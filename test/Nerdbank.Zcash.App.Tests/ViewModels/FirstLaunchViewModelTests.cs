// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Linq;

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
		Account account = Assert.Single(this.MainViewModel.Wallet.AllAccounts.SelectMany(a => a));
		Assert.False(string.IsNullOrEmpty(account.Name));
		this.logger.WriteLine(account.Name);
	}
}
