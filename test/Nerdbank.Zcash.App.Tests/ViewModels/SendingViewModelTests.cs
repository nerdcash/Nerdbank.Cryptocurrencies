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

		Assert.Equal(string.Empty, viewModel.LineItems[0].RecipientAddress);
		Assert.Equal("ZEC", viewModel.LineItems[0].TickerSymbol);
		Assert.Equal(0m, viewModel.LineItems[0].Amount);
		Assert.Equal(0.0001m, viewModel.Fee.Amount);
		Assert.Equal(0m, viewModel.Subtotal.Amount);
		Assert.Equal(viewModel.Subtotal.Amount + viewModel.Fee.Amount, viewModel.Total.Amount);
	}
}
