// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using Nerdbank.Cryptocurrencies.Exchanges;

public abstract class HistoricalPriceTestBase : TestBase
{
	protected static readonly TradingPair UsdZec = new(Security.USD, Security.ZEC);

	protected abstract IHistoricalExchangeRateProvider Provider { get; }

	protected virtual string? SkipGetExchangeRateTests => null;

	[Test, MatrixDataSource]
	public async Task GetExchangeRateAsync_RespectsPairOrdering(bool fiatSecond)
	{
		Skip.When(this.SkipGetExchangeRateTests is not null, this.SkipGetExchangeRateTests ?? "Not skipped");

		TradingPair pair = UsdZec;
		if (fiatSecond)
		{
			pair = pair.OppositeDirection;
		}

		ExchangeRate? rate = await this.Provider.GetExchangeRateAsync(pair, DateTimeOffset.Now, this.TimeoutToken);
		this.Logger.WriteLine($"{rate}");
		Assert.NotNull(rate);
		ExchangeRate exchangeRate = rate.GetValueOrDefault();
		Assert.Equal(exchangeRate.Basis.Security, pair.Basis);
		Assert.Equal(exchangeRate.TradeInterest.Security, pair.TradeInterest);
	}

	[Test]
	public async Task GetHistoricalPricing_TooFarBack()
	{
		DateTimeOffset when = DateTimeOffset.Parse("11/3/2005", CultureInfo.InvariantCulture);
		Assert.Null(await this.Provider.GetExchangeRateAsync(UsdZec, when, this.TimeoutToken));
	}

	[Test]
	public async Task GetHistoricalPricing_TooFarForward()
	{
		DateTimeOffset when = DateTimeOffset.Now.AddDays(2);
		Assert.Null(await this.Provider.GetExchangeRateAsync(UsdZec, when, this.TimeoutToken));
	}

	[Test]
	public async Task GetAvailableTradingPairsAsync()
	{
		IReadOnlyCollection<TradingPair> pairs = await this.Provider.GetAvailableTradingPairsAsync(this.TimeoutToken);
		Assert.NotEmpty(pairs);
		Assert.Contains(pairs, pair => pair.Basis == Security.ZEC || pair.TradeInterest == Security.ZEC);
		foreach (TradingPair pair in pairs)
		{
			this.Logger.WriteLine($"{pair}");
		}
	}

	[Test]
	public async Task GetHistoricalPricing_Now()
	{
		Skip.When(this.SkipGetExchangeRateTests is not null, this.SkipGetExchangeRateTests ?? "Not skipped");

		DateTimeOffset when = DateTimeOffset.Now;
		ExchangeRate? rate = await this.Provider.GetExchangeRateAsync(UsdZec, when, this.TimeoutToken);
		Assert.NotNull(rate);
		this.Logger.WriteLine($"{rate}");
	}
}
