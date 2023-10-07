// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Cryptocurrencies.Exchanges;

public class ExchangeRateTests : TestBase
{
	public ExchangeRateTests(ITestOutputHelper logger)
		: base(logger)
	{
	}

	[Fact]
	public void ToString_Formatting()
	{
		var basis = new SecurityAmount(30m, Security.USD);
		var tradeInterest = new SecurityAmount(1.5m, Security.ZEC);
		var rate = new ExchangeRate(basis, tradeInterest);
		string actual = rate.ToString();
		this.Logger.WriteLine(actual);
		Assert.Equal($"{basis} <=> {tradeInterest}", actual);
	}

	[Fact]
	public void InBasisAmount()
	{
		var basis = new SecurityAmount(30m, Security.USD);
		var tradeInterest = new SecurityAmount(1.5m, Security.ZEC);
		var rate = new ExchangeRate(basis, tradeInterest);
		SecurityAmount amountPerZEC = rate.InBasisAmount;
		Assert.Equal(new SecurityAmount(20m, Security.USD), amountPerZEC);
	}

	[Fact]
	public void OppositeDirection()
	{
		SecurityAmount basis = new(30m, Security.USD);
		SecurityAmount tradeInterest = new(1.5m, Security.ZEC);
		ExchangeRate rate = new(basis, tradeInterest);
		ExchangeRate opposite = rate.OppositeDirection;
		Assert.Equal(new ExchangeRate(tradeInterest, basis), opposite);
	}
}
