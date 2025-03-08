// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Cryptocurrencies.Exchanges;

public class TradingPairEitherOrderEqualityComparerTests
{
	[Fact]
	public void OrderDoesNotMatter()
	{
		TradingPair pair1 = new(Security.BTC, Security.USD);
		TradingPair pair2 = new(Security.USD, Security.BTC);
		Assert.NotEqual(pair1, pair2);
		Assert.NotEqual(pair1.GetHashCode(), pair2.GetHashCode());

		Assert.True(TradingPairEitherOrderEqualityComparer.Instance.Equals(pair1, pair2));
		Assert.Equal(TradingPairEitherOrderEqualityComparer.Instance.GetHashCode(pair1), TradingPairEitherOrderEqualityComparer.Instance.GetHashCode(pair2));
	}

	[Fact]
	public void DifferentTradingPairs()
	{
		TradingPair pair1 = new(Security.BTC, Security.USD);
		TradingPair pair2 = new(Security.BTC, Security.EUR);
		Assert.NotEqual(pair1, pair2);
		Assert.NotEqual(pair1.GetHashCode(), pair2.GetHashCode());

		Assert.False(TradingPairEitherOrderEqualityComparer.Instance.Equals(pair1, pair2));
		Assert.NotEqual(TradingPairEitherOrderEqualityComparer.Instance.GetHashCode(pair1), TradingPairEitherOrderEqualityComparer.Instance.GetHashCode(pair2));
	}
}
