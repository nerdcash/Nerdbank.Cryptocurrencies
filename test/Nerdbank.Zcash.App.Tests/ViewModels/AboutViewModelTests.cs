// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Linq;

namespace ViewModels;

public class AboutViewModelTests : ViewModelTestBase
{
	private readonly AboutViewModel viewModel;

	public AboutViewModelTests()
	{
		this.viewModel = new(this.MainViewModel);
	}

	[Fact]
	public async Task DonateEnabledIffNonEmptyWallet()
	{
		Assert.False(await this.viewModel.DonateCommand.CanExecute.FirstAsync());
		await this.InitializeWalletAsync();
		Assert.True(await this.viewModel.DonateCommand.CanExecute.FirstAsync());
	}
}
