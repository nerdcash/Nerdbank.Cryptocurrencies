// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Globalization;
using Microsoft.VisualStudio.Threading;

namespace Nerdbank.Cryptocurrencies.Exchanges;

#pragma warning disable IDE0008 // Use explicit type

/// <summary>
/// Provides historical exchange rates from Yahoo! Finance.
/// </summary>
/// <remarks>
/// Only historical prices are available from Yahoo! Finance. No real-time prices.
/// Historical prices are produced by taking the midpoint between the open and close prices for the day requested.
/// </remarks>
public class YahooFinance : IHistoricalExchangeRateProvider
{
	/// <summary>
	/// A URL format string that can be used to download historical prices from Yahoo Finance.
	/// </summary>
	/// <remarks>
	/// This was discovered by web search <see href="https://finance.yahoo.com/quote/ZEC-USD/history">this URL</see>.
	/// </remarks>
	private const string HistoricalPriceUrlFormatString = "https://query1.finance.yahoo.com/v7/finance/download/{0}?period1={1}&period2={2}&interval=1d&events=history&includeAdjustedClose=true";

	private static readonly DateTimeOffset UnixEpoch = new(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);

	private static readonly ImmutableHashSet<TradingPair> AvailableTradingPairs = new[]
	{
		// There are probably more. But I couldn't find an API to list them.
		new TradingPair(Security.USD, Security.ZEC),
		new TradingPair(Security.EUR, Security.ZEC),
		new TradingPair(Security.AUD, Security.ZEC),
		new TradingPair(Security.USD, Security.BTC),
		new TradingPair(Security.USD, Security.BCH),
		new TradingPair(Security.USD, Security.LTC),
		new TradingPair(Security.USD, Security.XNO),
	}.ToImmutableHashSet(TradingPairEitherOrderEqualityComparer.Instance);

	private readonly ConcurrentDictionary<TradingPair, ConcurrentDictionary<int, Task<IReadOnlyDictionary<DateOnly, ExchangeRate>>>> historicalExchangeRates = new(TradingPairEitherOrderEqualityComparer.Instance);
	private readonly HttpClient httpClient;

	/// <summary>
	/// Initializes a new instance of the <see cref="YahooFinance"/> class.
	/// </summary>
	/// <param name="httpClient">The HTTP client to use for downloading prices.</param>
	public YahooFinance(HttpClient httpClient)
	{
		this.httpClient = httpClient;
	}

	/// <inheritdoc/>
	public ValueTask<IReadOnlySet<TradingPair>> GetAvailableTradingPairsAsync(CancellationToken cancellationToken)
	{
		return new(AvailableTradingPairs);
	}

	/// <inheritdoc/>
	public async ValueTask<ExchangeRate> GetExchangeRateAsync(TradingPair tradingPair, DateTimeOffset when, CancellationToken cancellationToken)
	{
		if (!AvailableTradingPairs.TryGetValue(tradingPair, out TradingPair normalizedTradingPair))
		{
			throw new NotSupportedException(Strings.TradingPairNotSupported);
		}

		var byYear = this.historicalExchangeRates.GetOrAdd(
			normalizedTradingPair,
			static tp => new ConcurrentDictionary<int, Task<IReadOnlyDictionary<DateOnly, ExchangeRate>>>());

		var byDateInYear = await byYear.GetOrAdd(
			when.UtcDateTime.Year,
			static (int year, (TradingPair NormalizedTradingPair, HttpClient HttpClient) arg) => Task.Run(async delegate
			{
				Uri fetchUrl = GetHistoricalPricesDownloadUrl(
					arg.NormalizedTradingPair,
					new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero),
					new DateTimeOffset(year, 12, 31, 0, 0, 0, TimeSpan.Zero));

				// Do NOT use the cancellation token here since the Task can be shared across invocations.
				HttpResponseMessage response = await arg.HttpClient.GetAsync(fetchUrl).ConfigureAwait(false);
				if (!response.IsSuccessStatusCode)
				{
					string errorMessage = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
					throw new InvalidOperationException(
						Strings.HistoricalPriceNotAvailableForThisDate,
						new HttpRequestException(errorMessage, null, response.StatusCode));
				}

				using Stream csvStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
				using StreamReader csvReader = new(csvStream);

				// Skip the header line.
				await csvReader.ReadLineAsync().ConfigureAwait(false);

				Dictionary<DateOnly, ExchangeRate> prices = new();
				string? line;
				while ((line = await csvReader.ReadLineAsync().ConfigureAwait(false)) is not null)
				{
					string[] cells = line.Split(',');
					DateOnly when = DateOnly.FromDateTime(DateTime.Parse(cells[0], CultureInfo.InvariantCulture));
					decimal open = decimal.Parse(cells[1], CultureInfo.InvariantCulture);
					decimal close = decimal.Parse(cells[4], CultureInfo.InvariantCulture);
					decimal mid = (open + close) / 2;
					prices.Add(
						when,
						new ExchangeRate(
							arg.NormalizedTradingPair.Basis.Amount(mid),
							arg.NormalizedTradingPair.TradeInterest.Amount(1)));
				}

				return (IReadOnlyDictionary<DateOnly, ExchangeRate>)prices;
			}),
			(normalizedTradingPair, this.httpClient)).WithCancellation(cancellationToken).ConfigureAwait(false);

		if (!byDateInYear.TryGetValue(DateOnly.FromDateTime(when.UtcDateTime), out ExchangeRate rate))
		{
			throw new InvalidOperationException(Strings.HistoricalPriceNotAvailableForThisDate);
		}

		return tradingPair == normalizedTradingPair ? rate : rate.OppositeDirection;
	}

	private static Uri GetHistoricalPricesDownloadUrl(TradingPair tradingPair, DateTimeOffset start, DateTimeOffset end)
	{
		string uri = string.Format(
			HistoricalPriceUrlFormatString,
			$"{tradingPair.TradeInterest.TickerSymbol}-{tradingPair.Basis.TickerSymbol}",
			ToUnixTimeSeconds(start),
			ToUnixTimeSeconds(end));
		return new Uri(uri);
	}

	private static uint ToUnixTimeSeconds(DateTimeOffset dateTimeOffset) => (uint)(dateTimeOffset - UnixEpoch).TotalSeconds;
}
