// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Cryptocurrencies.Exchanges;

/// <summary>
/// A source of current exchange rates between various crypto and fiat currencies.
/// </summary>
public interface IExchangeRateProvider
{
	/// <summary>
	/// Gets the trading pairs that this exchange supports.
	/// </summary>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>A collection of trading pairs. The set is configured with an equality comparer that allows for matching a trading pair expressed in either order.</returns>
	ValueTask<IReadOnlySet<TradingPair>> GetAvailableTradingPairsAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Gets the value of one currency in terms of another currency.
	/// </summary>
	/// <param name="tradingPair">The trading pair to get the price of. The available pairs can be obtained from <see cref="GetAvailableTradingPairsAsync(CancellationToken)"/>.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The exchange rate for the trading pair, where the <see cref="ExchangeRate.Basis"/> security always matches the <see cref="TradingPair.Basis"/> on the <paramref name="tradingPair"/>.</returns>
	/// <exception cref="NotSupportedException">Thrown when the exchange does not support the requested trading pair.</exception>
	ValueTask<ExchangeRate> GetExchangeRateAsync(TradingPair tradingPair, CancellationToken cancellationToken);

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

		IReadOnlySet<TradingPair> availablePairs = await this.GetAvailableTradingPairsAsync(cancellationToken);

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
