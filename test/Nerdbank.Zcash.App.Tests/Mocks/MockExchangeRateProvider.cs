// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using Microsoft.VisualStudio.Threading;
using Nerdbank.Cryptocurrencies.Exchanges;

namespace Mocks;

internal class MockExchangeRateProvider : IExchangeRateProvider, IHistoricalExchangeRateProvider
{
	internal const decimal ZecPriceUsd = 30.50m;

	internal AsyncManualResetEvent PauseExchangeRateFetch { get; } = new(initialState: true);

	public ValueTask<IReadOnlySet<TradingPair>> GetAvailableTradingPairsAsync(CancellationToken cancellationToken)
	{
		return new ValueTask<IReadOnlySet<TradingPair>>(ImmutableHashSet.Create(new TradingPair(Security.USD, Security.ZEC)));
	}

	public async ValueTask<ExchangeRate> GetExchangeRateAsync(TradingPair tradingPair, CancellationToken cancellationToken)
	{
		await this.PauseExchangeRateFetch.WaitAsync(cancellationToken);
		return new ExchangeRate(tradingPair.Basis.Amount(ZecPriceUsd), tradingPair.TradeInterest.Amount(1));
	}

	public async ValueTask<ExchangeRate?> GetExchangeRateAsync(TradingPair tradingPair, DateTimeOffset when, CancellationToken cancellationToken)
	{
		await this.PauseExchangeRateFetch.WaitAsync(cancellationToken);
		return new ExchangeRate(tradingPair.Basis.Amount(ZecPriceUsd + when.Day), tradingPair.TradeInterest.Amount(1));
	}
}
