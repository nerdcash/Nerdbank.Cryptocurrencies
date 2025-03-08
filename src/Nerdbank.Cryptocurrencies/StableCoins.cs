// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

namespace Nerdbank.Cryptocurrencies;

/// <summary>
/// Utilities for working with stable coins.
/// </summary>
public static class StableCoins
{
	private static readonly List<IReadOnlySet<Security>> RelatedCoins = new()
	{
		SecuritySet(Security.USD, Security.USDT, Security.USDC, Security.BUSD),
	};

	/// <summary>
	/// Gets a list of crypto and fiat currencies that are pegged to have equivalent value with each other.
	/// </summary>
	/// <param name="security">A fiat or crypto currency.</param>
	/// <returns>A set of securities sharing the same peg.</returns>
	public static IReadOnlySet<Security> GetSecuritiesSharingPeg(Security security)
	{
		foreach (IReadOnlySet<Security> relatedCoins in RelatedCoins)
		{
			if (relatedCoins.Contains(security))
			{
				return relatedCoins;
			}
		}

		// A set of just one.
		return SecuritySet(security);
	}

	private static IReadOnlySet<Security> SecuritySet(params Security[] securities) => ImmutableHashSet.Create(SecuritySymbolOnlyComparer.Instance, securities);
}
