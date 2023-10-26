// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public class SendingViewModelTests : ViewModelTestBase
{
	private readonly SendingViewModel viewModel;

	public SendingViewModelTests()
	{
		this.viewModel = new(this.MainViewModel);
	}

	[Fact]
	public async Task InitialValues()
	{
		await this.InitializeWalletAsync();
		Assert.Equal(string.Empty, this.viewModel.RecipientAddress);
		Assert.Equal("ZEC", this.viewModel.TickerSymbol);
		Assert.Equal(0m, this.viewModel.Amount);
		Assert.Null(this.viewModel.Fee);
		Assert.Equal(0m, this.viewModel.Subtotal.Amount);
		Assert.Equal(0m, this.viewModel.Total.Amount);
	}
}
