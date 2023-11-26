﻿// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Cryptocurrencies.Exchanges;

/// <summary>
/// An equality comparer for <see cref="TradingPair"/> that considers the order of the <see cref="TradingPair.Basis"/> and <see cref="TradingPair.TradeInterest"/> properties to be irrelevant.
/// </summary>
public class TradingPairEitherOrderEqualityComparer : IEqualityComparer<TradingPair>
{
	/// <summary>
	/// Gets the singleton instance.
	/// </summary>
	public static readonly TradingPairEitherOrderEqualityComparer Instance = new();

	private TradingPairEitherOrderEqualityComparer()
	{
	}

	/// <inheritdoc/>
	public bool Equals(TradingPair x, TradingPair y) => x == y || x.OppositeDirection == y;

	/// <inheritdoc/>
	public int GetHashCode(TradingPair obj)
	{
		return StringComparer.OrdinalIgnoreCase.Compare(obj.Basis.TickerSymbol, obj.TradeInterest.TickerSymbol) < 0
			? StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Basis.TickerSymbol)
			: StringComparer.OrdinalIgnoreCase.GetHashCode(obj.TradeInterest.TickerSymbol);
	}
}
