// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Cryptocurrencies.Exchanges;

namespace Models;

public class ExchangeRateRecordTests : ModelTestBase<ExchangeRateRecord>
{
	public ExchangeRateRecordTests(ITestOutputHelper logger)
		: base(logger)
	{
	}

	public override ExchangeRateRecord Model { get; } = new();

	[Fact]
	public void Set_TryGet_SameTimeZone()
	{
		DateTimeOffset timestamp = new(2021, 1, 1, 0, 0, 0, TimeSpan.FromHours(7));
		ExchangeRate rate = new(Security.USD.Amount(30), Security.ZEC.Amount(1));
		this.Model.SetExchangeRate(timestamp, rate);

		// Try with original input data.
		Assert.True(this.Model.TryGetExchangeRate(timestamp, rate.TradingPair, out ExchangeRate rate2));
		Assert.Equal(rate, rate2);

		// Try with the trading pair in reverse direction.
		Assert.True(this.Model.TryGetExchangeRate(timestamp, rate.TradingPair.OppositeDirection, out rate2));
		Assert.Equal(rate, rate2);
	}

	[Fact]
	public void Set_TryGet_DifferentTimeZone()
	{
		DateTimeOffset timestamp = new(2021, 1, 1, 0, 0, 0, TimeSpan.FromHours(7));
		ExchangeRate rate = new(Security.USD.Amount(30), Security.ZEC.Amount(1));
		this.Model.SetExchangeRate(timestamp, rate);

		DateTimeOffset timestamp2 = timestamp.ToOffset(TimeSpan.FromHours(10));
		Assert.True(this.Model.TryGetExchangeRate(timestamp, rate.TradingPair, out ExchangeRate rate2));
		Assert.Equal(rate, rate2);
	}
}
