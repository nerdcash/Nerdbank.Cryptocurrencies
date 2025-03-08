// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Cryptocurrencies.Exchanges;

/// <summary>
/// An interface implemented by exchanges that support trading pairs.
/// </summary>
public interface ITradingPairProvider
{
	/// <summary>
	/// Gets the trading pairs that this exchange supports.
	/// </summary>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>A collection of trading pairs. The set is configured with an equality comparer that allows for matching a trading pair expressed in either order.</returns>
	ValueTask<IReadOnlySet<TradingPair>> GetAvailableTradingPairsAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Searches for the first trading pair that supports the given <paramref name="tradeInterest"/> and one of the <paramref name="acceptableBases"/>.
	/// </summary>
	/// <param name="tradeInterest">The security of interest (the one to be purchased).</param>
	/// <param name="acceptableBases">
	/// The securities that may be used to purchase the <paramref name="tradeInterest"/>.
	/// Using the result of <see cref="StableCoins.GetSecuritiesSharingPeg(Security)"/> may be useful to find the value of a cryptocurrency no matter what stablecoin the exchange may support.
	/// </param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The trading pair that was found; or <see langword="null" /> if no match was found.</returns>
	async ValueTask<TradingPair?> FindFirstSupportedTradingPairAsync(Security tradeInterest, IEnumerable<Security> acceptableBases, CancellationToken cancellationToken)
	{
		Requires.NotNull(acceptableBases);

		IReadOnlySet<TradingPair> availablePairs = await this.GetAvailableTradingPairsAsync(cancellationToken).ConfigureAwait(false);

		foreach (Security basis in acceptableBases)
		{
			TradingPair pair = new(basis, tradeInterest);
			if (availablePairs.Contains(pair))
			{
				return pair;
			}
		}

		return null;
	}
}
