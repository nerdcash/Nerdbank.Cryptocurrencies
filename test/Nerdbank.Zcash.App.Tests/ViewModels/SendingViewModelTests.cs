// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Reactive.Linq;
using Mocks;
using Nerdbank.Cryptocurrencies.Exchanges;

namespace ViewModels;

public class SendingViewModelTests : ViewModelTestBase
{
	private readonly ITestOutputHelper logger;
	private SendingViewModel viewModel = null!; // set in InitializeAsync

	public SendingViewModelTests(ITestOutputHelper logger)
	{
		this.MainViewModel.ExchangeRateProvider = new MockExchangeRateProvider();
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
		Assert.Equal("ZEC", this.viewModel.LineItems[0].TickerSymbol);
		Assert.Equal(0m, this.viewModel.LineItems[0].Amount);
		Assert.Equal(0.0001m, this.viewModel.Fee.Amount);
		Assert.Equal(0m, this.viewModel.Subtotal.Amount);
		Assert.Equal(this.viewModel.Subtotal.Amount + this.viewModel.Fee.Amount, this.viewModel.Total.Amount);
	}

	[Fact]
	public void Amount_SetUpdatesAlternateAmount()
	{
		SendingViewModel.LineItem lineItem = this.viewModel.LineItems[0];
		decimal zecAmount = 2;
		decimal usdAmount = zecAmount * MockExchangeRateProvider.ZecPriceUsd;
		lineItem.Amount = zecAmount;
		decimal roundedUsd = Security.USD.Amount(usdAmount).RoundedAmount;
		this.logger.WriteLine($"{roundedUsd} USD");
		Assert.Equal(zecAmount, lineItem.Amount);
		Assert.Equal(roundedUsd, lineItem.AmountInAlternateCurrency);
	}

	[Fact]
	public void AmountInAlternateCurrency_SetUpdatesAmount()
	{
		SendingViewModel.LineItem lineItem = this.viewModel.LineItems[0];
		decimal usdAmount = 60;
		decimal zecAmount = usdAmount / MockExchangeRateProvider.ZecPriceUsd;
		lineItem.AmountInAlternateCurrency = usdAmount;
		decimal roundedZec = Security.ZEC.Amount(zecAmount).RoundedAmount;
		this.logger.WriteLine($"{roundedZec} ZEC");
		Assert.Equal(usdAmount, lineItem.AmountInAlternateCurrency);
		Assert.Equal(roundedZec, lineItem.Amount);
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
	public async Task LateComingExchangeRateReconcilesAmounts()
	{
		// Arrange for the ExchangeRate to be not (yet) available.
		((MockExchangeRateProvider)this.MainViewModel.ExchangeRateProvider).PauseExchangeRateFetch.Reset();
		this.viewModel = new SendingViewModel(this.MainViewModel);
		SendingViewModel.LineItem lineItem = this.viewModel.LineItems[0];
		lineItem.Amount = 2;

		// Assert that the AmountInAlternateCurrency is not available.
		Assert.Null(lineItem.AmountInAlternateCurrency);

		// Arrange to notice when the AmountInAlternateCurrency property becomes available.
		Task alternateSet = WatchForAsync(lineItem, nameof(lineItem.AmountInAlternateCurrency), () => lineItem.AmountInAlternateCurrency is not null, this.TimeoutToken);

		// Publish the exchange rate
		((MockExchangeRateProvider)this.MainViewModel.ExchangeRateProvider).PauseExchangeRateFetch.Set();

		// Assert that AmountInAlternateCurrency is now available and set to the correct value.
		await alternateSet;

		Assert.Equal(2 * MockExchangeRateProvider.ZecPriceUsd, lineItem.AmountInAlternateCurrency);
	}

	[Fact]
	public void AlternateAmountsHiddenOnTestNetAccounts()
	{
		this.viewModel.SelectedAccount = this.MainViewModel.Wallet.Accounts.Single(a => a.Network == ZcashNetwork.TestNet);
		Assert.Null(this.viewModel.TotalAlternate);
	}

	private static Task WatchForAsync<T>(T owner, string propertyName, Func<bool> tester, CancellationToken cancellationToken)
		where T : INotifyPropertyChanged
	{
		if (tester())
		{
			return Task.CompletedTask;
		}

		CancellationTokenRegistration ctr = default;

		TaskCompletionSource<bool> tcs = new();
		owner.PropertyChanged += OnPropertyChange;

		ctr = cancellationToken.Register(() =>
		{
			owner.PropertyChanged -= OnPropertyChange;
			tcs.TrySetCanceled(cancellationToken);
		});

		return tcs.Task;

		void OnPropertyChange(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == propertyName && tester())
			{
				owner.PropertyChanged -= OnPropertyChange;
				tcs.TrySetResult(true);
				ctr.Dispose();
			}
		}
	}
}
