// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Cryptocurrencies.Exchanges;

public class BinanceUSExchangeTests : TestBase
{
	private readonly BinanceUSExchange exchange = new(new HttpClient());

	[Test, MatrixDataSource]
	[Property("RequiresNetwork", "true")]
	public async Task GetExchangeRateAsync_RespectsPairOrdering(bool fiatSecond)
	{
		TradingPair pair = new(Security.USDT, Security.ZEC);
		if (fiatSecond)
		{
			pair = pair.OppositeDirection;
		}

		ExchangeRate rate = await this.exchange.GetExchangeRateAsync(pair, this.TimeoutToken);
		this.Logger.WriteLine($"{rate}");
		Assert.Equal(rate.Basis.Security, pair.Basis);
		Assert.Equal(rate.TradeInterest.Security, pair.TradeInterest);
	}

	[Test]
	public void PricesAsOf_Initially() => Assert.Null(this.exchange.PricesAsOf);

	[Test]
	[Property("RequiresNetwork", "true")]
	public async Task PricesAsOf_AfterRefresh()
	{
		await this.exchange.RefreshPricesAsync(this.TimeoutToken);
		Assert.NotNull(this.exchange.PricesAsOf);
		Assert.True(DateTimeOffset.UtcNow - this.exchange.PricesAsOf < UnexpectedTimeout);
	}

	[Test]
	[Property("RequiresNetwork", "true")]
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
