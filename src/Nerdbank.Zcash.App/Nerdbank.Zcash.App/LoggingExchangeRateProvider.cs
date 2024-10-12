// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;
using Nerdbank.Cryptocurrencies.Exchanges;

namespace Nerdbank.Zcash.App;

/// <summary>
/// Adds logging to an existing exchange rate provider.
/// </summary>
/// <param name="inner">The actual exchange rate provider.</param>
/// <param name="logger">The logger to use.</param>
public class LoggingExchangeRateProvider(IExchangeRateProvider inner, ILogger logger) : IExchangeRateProvider
{
	public ValueTask<IReadOnlySet<TradingPair>> GetAvailableTradingPairsAsync(CancellationToken cancellationToken)
	{
		return inner.GetAvailableTradingPairsAsync(cancellationToken);
	}

	public async ValueTask<ExchangeRate> GetExchangeRateAsync(TradingPair tradingPair, CancellationToken cancellationToken)
	{
		ExchangeRate result = await inner.GetExchangeRateAsync(tradingPair, cancellationToken);
		logger.LogInformation("Trading pair {tradingPair} has exchange rate of {rate}.", tradingPair, result);
		return result;
	}
}
