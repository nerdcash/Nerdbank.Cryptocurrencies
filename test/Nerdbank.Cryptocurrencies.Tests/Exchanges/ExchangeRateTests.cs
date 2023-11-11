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

	[Fact]
	public void Asset_Times_ExchangeRate()
	{
		ExchangeRate usdToZec = new(new SecurityAmount(30m, Security.USD), new SecurityAmount(1m, Security.ZEC));

		SecurityAmount zec = Security.ZEC.Amount(2);
		Assert.Equal(Security.USD.Amount(60), zec * usdToZec);
		Assert.Equal(Security.USD.Amount(60), zec * usdToZec.OppositeDirection);
	}

	[Fact]
	public void UnrelatedAsset_Times_ExchangeRate()
	{
		ExchangeRate usdToZec = new(new SecurityAmount(30m, Security.USD), new SecurityAmount(1m, Security.ZEC));
		Assert.Throws<ArgumentException>(() => Security.BTC.Amount(1) * usdToZec);
	}

	[Fact]
	public void ExchangeRate_Times_ExchangeRate1()
	{
		ExchangeRate usdToZec = new(new SecurityAmount(60m, Security.USD), new SecurityAmount(2m, Security.ZEC));
		ExchangeRate usdToBtc = new(new SecurityAmount(45_000m, Security.USD), new SecurityAmount(1.5m, Security.BTC));

		ExchangeRate zecToBtc = usdToZec * usdToBtc;
		Assert.Equal(Security.ZEC, zecToBtc.Basis.Security);
		Assert.Equal(Security.BTC, zecToBtc.TradeInterest.Security);

		SecurityAmount zec = Security.ZEC.Amount(500);
		SecurityAmount btc = zec * zecToBtc;
		Assert.Equal(Security.BTC.Amount(0.5m), btc);
	}

	[Fact]
	public void ExchangeRate_Times_ExchangeRate2()
	{
		ExchangeRate usdToZec = new(new SecurityAmount(60m, Security.USD), new SecurityAmount(2m, Security.ZEC));
		ExchangeRate usdToBtc = new(new SecurityAmount(45_000m, Security.USD), new SecurityAmount(1.5m, Security.BTC));

		ExchangeRate zecToBtc = usdToZec * usdToBtc.OppositeDirection;
		Assert.Equal(Security.ZEC, zecToBtc.Basis.Security);
		Assert.Equal(Security.BTC, zecToBtc.TradeInterest.Security);

		SecurityAmount zec = Security.ZEC.Amount(500);
		SecurityAmount btc = zec * zecToBtc;
		Assert.Equal(Security.BTC.Amount(0.5m), btc);
	}

	[Fact]
	public void ExchangeRate_Times_ExchangeRate3()
	{
		ExchangeRate usdToZec = new(new SecurityAmount(60m, Security.USD), new SecurityAmount(2m, Security.ZEC));
		ExchangeRate usdToBtc = new(new SecurityAmount(45_000m, Security.USD), new SecurityAmount(1.5m, Security.BTC));

		ExchangeRate zecToBtc = usdToZec.OppositeDirection * usdToBtc;
		Assert.Equal(Security.ZEC, zecToBtc.Basis.Security);
		Assert.Equal(Security.BTC, zecToBtc.TradeInterest.Security);

		SecurityAmount zec = Security.ZEC.Amount(500);
		SecurityAmount btc = zec * zecToBtc;
		Assert.Equal(Security.BTC.Amount(0.5m), btc);
	}

	[Fact]
	public void ExchangeRate_Times_ExchangeRate4()
	{
		ExchangeRate usdToZec = new(new SecurityAmount(60m, Security.USD), new SecurityAmount(2m, Security.ZEC));
		ExchangeRate usdToBtc = new(new SecurityAmount(45_000m, Security.USD), new SecurityAmount(1.5m, Security.BTC));

		ExchangeRate zecToBtc = usdToZec.OppositeDirection * usdToBtc.OppositeDirection;
		Assert.Equal(Security.ZEC, zecToBtc.Basis.Security);
		Assert.Equal(Security.BTC, zecToBtc.TradeInterest.Security);

		SecurityAmount zec = Security.ZEC.Amount(500);
		SecurityAmount btc = zec * zecToBtc;
		Assert.Equal(Security.BTC.Amount(0.5m), btc);
	}

	[Fact]
	public void ExchangeRate_Times_UnrelatedExchangeRate()
	{
		ExchangeRate usdToZec = new(new SecurityAmount(60m, Security.USD), new SecurityAmount(2m, Security.ZEC));
		ExchangeRate ethToBtc = new(new SecurityAmount(45m, Security.ETH), new SecurityAmount(1.5m, Security.BTC));
		Assert.Throws<ArgumentException>(() => usdToZec * ethToBtc);
	}

	[Fact]
	public void ExchangeRate_Times_ExchangeRateWithSameUnits()
	{
		ExchangeRate usdToZec = new(new SecurityAmount(60m, Security.USD), new SecurityAmount(2m, Security.ZEC));
		Assert.Throws<ArgumentException>(() => usdToZec * usdToZec);
	}

	[Fact]
	public void ExchangeRateChain()
	{
		SecurityAmount zec = Security.ZEC.Amount(5);
		ExchangeRate usdZec = new(Security.USD.Amount(60m), Security.ZEC.Amount(2m));
		ExchangeRate btcUsd = new(Security.USD.Amount(45_000m), Security.BTC.Amount(1.5m));
		ExchangeRate ethBtc = new(Security.ETH.Amount(45m), Security.BTC.Amount(2.5m));
		ExchangeRate ltcEth = new(Security.LTC.Amount(60m), Security.ETH.Amount(3m));
		SecurityAmount ltc = zec * usdZec * btcUsd * ethBtc * ltcEth;
		Assert.Equal(Security.LTC.Amount(1.8m), ltc);
	}

	[Fact]
	public void MultiplyByScalar()
	{
		Assert.Equal(Rate(2, 4), Rate(1, 2) * 2);

		ExchangeRate Rate(decimal basis, decimal tradeInterest) => new(Security.USD.Amount(basis), Security.ZEC.Amount(tradeInterest));
	}

	[Fact]
	public void Normalized()
	{
		Assert.Equal(Rate(30, 1), Rate(60, 2).Normalized);
		Assert.Equal(Rate(30, 1), Rate(15, 0.5m).Normalized);

		ExchangeRate Rate(decimal basis, decimal tradeInterest) => new(Security.USD.Amount(basis), Security.ZEC.Amount(tradeInterest));
	}
}
