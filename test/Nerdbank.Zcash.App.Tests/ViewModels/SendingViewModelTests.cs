// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Linq;
using Mocks;

namespace ViewModels;

public class SendingViewModelTests : ViewModelTestBase
{
	private readonly ITestOutputHelper logger;
	private SendingViewModel viewModel = null!; // set in InitializeAsync

	public SendingViewModelTests(ITestOutputHelper logger)
	{
		this.logger = logger;
	}

	public override async Task InitializeAsync()
	{
		await base.InitializeAsync();
		await this.InitializeWalletAsync();
		this.viewModel = new SendingViewModel(this.MainViewModel);
	}

	[Fact]
	public void InitialValues()
	{
		Assert.Equal(string.Empty, this.viewModel.LineItems[0].RecipientAddress);
		Assert.Equal("ZEC", this.viewModel.LineItems[0].AmountEntry.TickerSymbol);
		Assert.Null(this.viewModel.LineItems[0].Amount);
		Assert.Null(this.viewModel.Fee);
		Assert.Equal(0m, this.viewModel.Subtotal.Amount);
		Assert.Equal(this.viewModel.Subtotal.Amount + this.viewModel.Fee?.Amount, this.viewModel.Total?.Amount);
	}

	[Fact]
	public async Task LateComingExchangeRateUpdatesAggregates()
	{
		// Arrange for the ExchangeRate to be not (yet) available.
		((MockExchangeRateProvider)this.MainViewModel.ExchangeRateProvider).PauseExchangeRateFetch.Reset();
		this.viewModel = new SendingViewModel(this.MainViewModel);
		this.viewModel.LineItems[0].Amount = 2;

		// Assert that the SubtotalAlternate is not available.
		Assert.Null(this.viewModel.SubtotalAlternate);

		// Arrange to notice when the SubtotalAlternate property becomes available.
		Task subtotalAlternateSet = WatchForAsync(this.viewModel, nameof(this.viewModel.SubtotalAlternate), () => this.viewModel.SubtotalAlternate is not null, this.TimeoutToken);

		// Publish the exchange rate
		((MockExchangeRateProvider)this.MainViewModel.ExchangeRateProvider).PauseExchangeRateFetch.Set();

		// Assert that SubtotalAlternate is now available and set to the correct value.
		await subtotalAlternateSet;
		Assert.Equal(2 * MockExchangeRateProvider.ZecPriceUsd, this.viewModel.SubtotalAlternate?.Amount);
	}

	[Fact]
	public void AlternateAmountsHiddenOnTestNetAccounts()
	{
		this.viewModel.SelectedAccount = this.MainViewModel.Wallet.Accounts.Single(a => a.Network == ZcashNetwork.TestNet);
		Assert.Null(this.viewModel.TotalAlternate);
	}
}
