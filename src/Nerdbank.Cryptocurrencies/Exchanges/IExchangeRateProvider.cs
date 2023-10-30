// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Cryptocurrencies.Exchanges;

/// <summary>
/// A source of current exchange rates between various crypto and fiat currencies.
/// </summary>
public interface IExchangeRateProvider : ITradingPairProvider
{
	/// <summary>
	/// Gets the value of one currency in terms of another currency.
	/// </summary>
	/// <param name="tradingPair">The trading pair to get the price of. The available pairs can be obtained from <see cref="ITradingPairProvider.GetAvailableTradingPairsAsync(CancellationToken)"/>.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The exchange rate for the trading pair, where the <see cref="ExchangeRate.Basis"/> security always matches the <see cref="TradingPair.Basis"/> on the <paramref name="tradingPair"/>.</returns>
	/// <exception cref="NotSupportedException">Thrown when the exchange does not support the requested trading pair.</exception>
	ValueTask<ExchangeRate> GetExchangeRateAsync(TradingPair tradingPair, CancellationToken cancellationToken);
}
