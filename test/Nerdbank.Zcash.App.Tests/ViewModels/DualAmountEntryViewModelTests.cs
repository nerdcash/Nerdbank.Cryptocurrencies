// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Mocks;
using Nerdbank.Cryptocurrencies.Exchanges;

namespace ViewModels;

public class DualAmountEntryViewModelTests : ViewModelTestBase
{
	private readonly ITestOutputHelper logger;

	public DualAmountEntryViewModelTests(ITestOutputHelper logger)
	{
		this.logger = logger;
	}

	public override async Task InitializeAsync()
	{
		await base.InitializeAsync();
		await this.InitializeWalletAsync();
	}

	[UIFact]
	public void Amount_SetUpdatesAlternateAmount()
	{
		DualAmountEntryViewModel viewModel = new(this.MainViewModel);
		decimal zecAmount = 2;
		decimal usdAmount = zecAmount * MockExchangeRateProvider.ZecPriceUsd;
		viewModel.Amount = zecAmount;
		decimal roundedUsd = Security.USD.Amount(usdAmount).RoundedAmount;
		this.logger.WriteLine($"{roundedUsd} USD");
		Assert.Equal(zecAmount, viewModel.Amount);
		Assert.Equal(roundedUsd, viewModel.AmountInAlternateCurrency);
	}

	[UIFact]
	public void AmountInAlternateCurrency_SetUpdatesAmount()
	{
		DualAmountEntryViewModel viewModel = new(this.MainViewModel);
		decimal usdAmount = 60;
		decimal zecAmount = usdAmount / MockExchangeRateProvider.ZecPriceUsd;
		viewModel.AmountInAlternateCurrency = usdAmount;
		decimal roundedZec = Security.ZEC.Amount(zecAmount).RoundedAmount;
		this.logger.WriteLine($"{roundedZec} ZEC");
		Assert.Equal(usdAmount, viewModel.AmountInAlternateCurrency);
		Assert.Equal(roundedZec, viewModel.Amount);
	}

	[UIFact]
	public async Task LateComingExchangeRateReconcilesAmounts()
	{
		// Arrange for the ExchangeRate to be not (yet) available.
		((MockExchangeRateProvider)this.MainViewModel.ExchangeRateProvider).PauseExchangeRateFetch.Reset();
		DualAmountEntryViewModel viewModel = new(this.MainViewModel);
		viewModel.Amount = 2;

		// Assert that the AmountInAlternateCurrency is not available.
		Assert.Null(viewModel.AmountInAlternateCurrency);

		// Arrange to notice when the AmountInAlternateCurrency property becomes available.
		Task alternateSet = WatchForAsync(viewModel, nameof(viewModel.AmountInAlternateCurrency), () => viewModel.AmountInAlternateCurrency is not null, this.TimeoutToken);

		// Publish the exchange rate
		((MockExchangeRateProvider)this.MainViewModel.ExchangeRateProvider).PauseExchangeRateFetch.Set();

		// Assert that AmountInAlternateCurrency is now available and set to the correct value.
		await alternateSet;

		Assert.Equal(2 * MockExchangeRateProvider.ZecPriceUsd, viewModel.AmountInAlternateCurrency);
	}

	[UIFact]
	public void AlternateAmountsHiddenOnTestNetAccounts()
	{
		DualAmountEntryViewModel viewModel = new(this.MainViewModel);
		viewModel.SelectedAccount = this.MainViewModel.Wallet.Accounts.Single(a => a.Network == ZcashNetwork.TestNet);
		Assert.Null(viewModel.AmountInAlternateCurrency);
	}
}
