// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Linq;

public class FirstLaunchViewModelTests
{
	private readonly MainViewModel mainViewModel = new();
	private FirstLaunchViewModel viewModel;

	public FirstLaunchViewModelTests()
	{
		this.viewModel = new(this.mainViewModel);
	}

	[Fact]
	public async Task CreateNewWallet()
	{
		await this.viewModel.StartNewWalletCommand.Execute().FirstAsync();
	}
}
