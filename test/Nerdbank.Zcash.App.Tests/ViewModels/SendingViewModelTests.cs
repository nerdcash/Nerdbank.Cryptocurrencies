// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ViewModels;

public class SendingViewModelTests : ViewModelTestBase
{
	[Fact]
	public async Task InitialValues()
	{
		await this.InitializeWalletAsync();
		SendingViewModel viewModel = new(this.MainViewModel);

		Assert.Equal(string.Empty, viewModel.RecipientAddress);
		Assert.Equal("ZEC", viewModel.TickerSymbol);
		Assert.Equal(0m, viewModel.Amount);
		Assert.Null(viewModel.Fee);
		Assert.Equal(0m, viewModel.Subtotal.Amount);
		Assert.Equal(0m, viewModel.Total.Amount);
	}
}
