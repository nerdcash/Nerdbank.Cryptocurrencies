// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Cryptocurrencies.Exchanges;

public class TradingPairTests
{
	[Fact]
	public void OppositeDirection()
	{
		TradingPair pair = new(Security.USD, Security.BTC);

		TradingPair expected = new(Security.BTC, Security.USD);
		TradingPair actual = pair.OppositeDirection;

		Assert.Equal(expected, actual);
	}
}
