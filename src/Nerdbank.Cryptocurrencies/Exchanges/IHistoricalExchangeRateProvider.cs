// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Cryptocurrencies.Exchanges;

/// <summary>
/// A source of current and historical exchange rates between various crypto and fiat currencies.
/// </summary>
public interface IHistoricalExchangeRateProvider : ITradingPairProvider
{
	/// <summary>
	/// Gets the exchange rate between two currencies at a specific point in time.
	/// </summary>
	/// <param name="tradingPair">The trading pair to get the price of.</param>
	/// <param name="when">The point in time to get the exchange rate for.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The exchange rate, or <see langword="null" /> if the exchange rate is not available for the requested time.</returns>
	/// <exception cref="NotSupportedException">Thrown when the trading pair is not offered from this provider.</exception>
	ValueTask<ExchangeRate?> GetExchangeRateAsync(TradingPair tradingPair, DateTimeOffset when, CancellationToken cancellationToken);
}
