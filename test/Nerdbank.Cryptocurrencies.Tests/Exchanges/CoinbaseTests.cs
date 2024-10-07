// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Cryptocurrencies.Exchanges;

[Trait("RequiresNetwork", "true")]
public class CoinbaseTests(ITestOutputHelper logger) : HistoricalPriceTestBase(logger)
{
	private readonly Coinbase exchange = new(new HttpClient() { DefaultRequestHeaders = { { "User-Agent", "Nerdbank.Cryptocurrencies.Tests" } } });

	protected override IHistoricalExchangeRateProvider Provider => this.exchange;

	[Fact]
	public async Task GetExchangeRateAsync()
	{
		DateTimeOffset when = new DateTimeOffset(2024, 10, 6, 13, 23, 5, 0, TimeSpan.Zero);
		ExchangeRate? rate = await this.exchange.GetExchangeRateAsync(UsdZec, when, this.TimeoutToken);
		this.Logger.WriteLine($"{rate}");
		Assert.NotNull(rate);
		Assert.Equal(Security.USD, rate.Value.Basis.Security);
		Assert.Equal(29.08m, rate.Value.InBasisAmount.RoundedAmount);
	}
}
