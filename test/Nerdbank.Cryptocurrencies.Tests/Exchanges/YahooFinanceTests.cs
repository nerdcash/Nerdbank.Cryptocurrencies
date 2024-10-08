// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using Nerdbank.Cryptocurrencies.Exchanges;

[Trait("RequiresNetwork", "true")]
public class YahooFinanceTests(ITestOutputHelper logger) : HistoricalPriceTestBase(logger)
{
	private readonly YahooFinance exchange = new(new HttpClient() { DefaultRequestHeaders = { { "User-Agent", "Nerdbank.Cryptocurrencies.Tests" } } });

	protected override IHistoricalExchangeRateProvider Provider => this.exchange;

	protected override string? SkipGetExchangeRateTests => "Authentication now required.";

	[Fact(Skip = "Authentication now required.")]
	public async Task GetZecUsdHistoricalPricing()
	{
		DateTimeOffset when = DateTimeOffset.Parse("11/3/2022", CultureInfo.InvariantCulture);
		ExchangeRate? exchangeRate = await this.exchange.GetExchangeRateAsync(UsdZec, when, this.TimeoutToken);
		this.Logger.WriteLine($"{when:d} {exchangeRate}");
		Assert.Equal(Security.USD.Amount((50.312881m + 50.36557m) / 2), exchangeRate.Value.InBasisAmount);
	}
}
