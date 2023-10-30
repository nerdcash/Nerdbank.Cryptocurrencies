// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using Nerdbank.Cryptocurrencies.Exchanges;

public class YahooFinanceTests : TestBase
{
	private static readonly TradingPair UsdZec = new(Security.USD, Security.ZEC);
	private readonly YahooFinance exchange = new(new HttpClient());

	public YahooFinanceTests(ITestOutputHelper logger)
		: base(logger)
	{
	}

	[Theory, PairwiseData]
	[Trait("RequiresNetwork", "true")]
	public async Task GetExchangeRateAsync_RespectsPairOrdering(bool fiatSecond)
	{
		TradingPair pair = new(Security.USD, Security.ZEC);
		if (fiatSecond)
		{
			pair = pair.OppositeDirection;
		}

		ExchangeRate rate = await this.exchange.GetExchangeRateAsync(pair, DateTimeOffset.Now, this.TimeoutToken);
		this.Logger.WriteLine($"{rate}");
		Assert.Equal(rate.Basis.Security, pair.Basis);
		Assert.Equal(rate.TradeInterest.Security, pair.TradeInterest);
	}

	[Fact]
	public async Task GetZecUsdHistoricalPricing()
	{
		DateTimeOffset when = DateTimeOffset.Parse("11/3/2022", CultureInfo.InvariantCulture);
		ExchangeRate exchangeRate = await this.exchange.GetExchangeRateAsync(UsdZec, when, this.TimeoutToken);
		this.Logger.WriteLine($"{when:d} {exchangeRate}");
		Assert.Equal(Security.USD.Amount((50.312881m + 50.36557m) / 2), exchangeRate.InBasisAmount);
	}

	[Fact]
	public async Task GetHistoricalPricing_TooFarBack()
	{
		DateTimeOffset when = DateTimeOffset.Parse("11/3/2005", CultureInfo.InvariantCulture);
		InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await this.exchange.GetExchangeRateAsync(UsdZec, when, this.TimeoutToken));
		this.Logger.WriteLine(ex.ToString());
	}

	[Fact]
	////[Trait("RequiresNetwork", "true")]
	public async Task GetAvailableTradingPairsAsync()
	{
		IReadOnlyCollection<TradingPair> pairs = await this.exchange.GetAvailableTradingPairsAsync(this.TimeoutToken);
		Assert.NotEmpty(pairs);
		foreach (TradingPair pair in pairs)
		{
			this.Logger.WriteLine($"{pair}");
		}
	}
}
