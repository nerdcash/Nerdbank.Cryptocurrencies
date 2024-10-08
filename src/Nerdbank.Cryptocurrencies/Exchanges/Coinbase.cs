// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.VisualStudio.Threading;

namespace Nerdbank.Cryptocurrencies.Exchanges;

/// <summary>
/// Obtains historical exchange rates from Coinbase.
/// </summary>
/// <remarks>
/// As an implementation detail, this provider only retrieves hourly granularity data, in chunks for 24 hour periods.
/// This aids both in maintaining privacy (e.g. not revealing the exact time of a trade) and in reducing the number of requests made to the Coinbase API.
/// </remarks>
public class Coinbase : IHistoricalExchangeRateProvider
{
	private static readonly ImmutableHashSet<TradingPair> EmptyTradingPairs = ImmutableHashSet.Create<TradingPair>(TradingPairEitherOrderEqualityComparer.Instance);

	private readonly AsyncLazy<ImmutableHashSet<TradingPair>> availableTradingPairs;
	private readonly HttpClient httpClient;
	private readonly ConcurrentDictionary<TradingPair, ConcurrentDictionary<DateOnly, Task<SortedList<DateTimeOffset, ExchangeRate>>>> historicalExchangeRates = new(TradingPairEitherOrderEqualityComparer.Instance);

	/// <summary>
	/// Initializes a new instance of the <see cref="Coinbase"/> class.
	/// </summary>
	/// <param name="httpClient">The HTTP client to use for downloading prices.</param>
	public Coinbase(HttpClient httpClient)
	{
		this.httpClient = httpClient;
		this.availableTradingPairs = new(() => this.GetAvailableTradingPairsNowAsync(CancellationToken.None));
	}

	/// <summary>
	/// These are the granularities supported by the Coinbase API.
	/// </summary>
	private enum Granularity
	{
#pragma warning disable SA1602 // Enumeration items should be documented
		Minute = 60,
		FiveMinutes = 300,
		FifteenMinutes = 900,
		Hourly = 3600,
		SixHours = 21600,
		Daily = 86400,
#pragma warning restore SA1602 // Enumeration items should be documented
	}

	/// <inheritdoc/>
	public async ValueTask<IReadOnlySet<TradingPair>> GetAvailableTradingPairsAsync(CancellationToken cancellationToken)
	{
		return await this.availableTradingPairs.GetValueAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async ValueTask<ExchangeRate?> GetExchangeRateAsync(TradingPair tradingPair, DateTimeOffset when, CancellationToken cancellationToken)
	{
		TradingPair normalizedTradingPair = await this.GetNormalizedPairAsync(tradingPair, cancellationToken).ConfigureAwait(false);

		ConcurrentDictionary<DateOnly, Task<SortedList<DateTimeOffset, ExchangeRate>>> byDate = this.historicalExchangeRates.GetOrAdd(
			normalizedTradingPair,
			static tp => new());

		SortedList<DateTimeOffset, ExchangeRate> forDate = await byDate.GetOrAdd(
			DateOnly.FromDateTime(when.UtcDateTime),
			static (DateOnly utcDate, (TradingPair TradingPair, Coinbase Self) arg) => Task.Run(async delegate
			{
				ResponseItem[] rows = await arg.Self.FetchCandlesAsync(arg.TradingPair, Granularity.Hourly, ToOffset(utcDate), ToOffset(utcDate.AddDays(1)), CancellationToken.None).ConfigureAwait(false);

				SortedList<DateTimeOffset, ExchangeRate> list = new();

				foreach (ResponseItem item in rows)
				{
					double price = (item.Open + item.Close) / 2;
					SecurityAmount basis = arg.TradingPair.Basis.Amount(1);
					SecurityAmount tradeInterest = arg.TradingPair.TradeInterest.Amount((decimal)price);
					list.Add(item.StartTime, new ExchangeRate(basis, tradeInterest));
				}

				return list;
			}),
			(normalizedTradingPair, this)).WithCancellation(cancellationToken).ConfigureAwait(false);

		ExchangeRate? result;
		if (TryFindBestMatch(forDate, when, out ExchangeRate rate))
		{
			result = rate;
		}
		else
		{
			// Allow for the best match that exceeds the time if no match that precedes it was found.
			result = forDate.Count > 0 ? forDate.GetValueAtIndex(0) : null;
		}

		return tradingPair == normalizedTradingPair ? result : result?.OppositeDirection;
	}

	/// <summary>
	/// Retrieves the best match for a key in a sorted list.
	/// For non-exact matches, the closest match that precedes the key is returned.
	/// </summary>
	/// <typeparam name="TKey">The type of the keys in the sorted list.</typeparam>
	/// <typeparam name="TValue">The type of the values in the sorted list.</typeparam>
	/// <param name="list">The sorted list to search.</param>
	/// <param name="key">The key to search for.</param>
	/// <param name="value">The best match for the key in the sorted list.</param>
	/// <returns><see langword="true" /> if a match or an element less than <paramref name="key"/> was found; otherwise <see langword="false"/>.</returns>
	private static bool TryFindBestMatch<TKey, TValue>(SortedList<TKey, TValue> list, TKey key, [MaybeNullWhen(false)] out TValue value)
		where TKey : notnull
	{
		int lower = 0, upper = list.Count - 1;

		while (lower <= upper)
		{
			int middle = lower + ((upper - lower) / 2);
			int comparison = list.Comparer.Compare(list.Keys[middle], key);

			switch (comparison)
			{
				case 0:
					value = list.Values[middle];
					return true;
				case < 0:
					lower = middle + 1;
					break;
				case > 0:
					upper = middle - 1;
					break;
			}
		}

		if (upper >= 0 && upper < list.Count)
		{
			value = list.Values[upper];
			return true;
		}

		value = default;
		return false;
	}

	private static DateTimeOffset ToOffset(DateOnly date) => new(date, TimeOnly.MinValue, TimeSpan.Zero);

	private async ValueTask<TradingPair> GetNormalizedPairAsync(TradingPair tradingPair, CancellationToken cancellationToken)
	{
		ImmutableHashSet<TradingPair> availableTradingPairs = await this.availableTradingPairs.GetValueAsync(cancellationToken).ConfigureAwait(false);
		if (!availableTradingPairs.TryGetValue(tradingPair, out TradingPair normalizedTradingPair))
		{
			throw new NotSupportedException(Strings.TradingPairNotSupported);
		}

		return normalizedTradingPair;
	}

	private async Task<ResponseItem[]> FetchCandlesAsync(TradingPair tradingPair, Granularity granularity, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken)
	{
		tradingPair = await this.GetNormalizedPairAsync(tradingPair, cancellationToken).ConfigureAwait(false);

		// https://docs.cdp.coinbase.com/exchange/reference/exchangerestapi_getproductcandles
		string requestUri = $"https://api.exchange.coinbase.com/products/{tradingPair.Basis.TickerSymbol}-{tradingPair.TradeInterest.TickerSymbol}/candles?granularity={(int)granularity}&start={start.ToUnixTimeSeconds()}&end={end.ToUnixTimeSeconds()}";

		ResponseItem[]? result = await this.httpClient.GetFromJsonAsync<ResponseItem[]>(requestUri, cancellationToken).ConfigureAwait(false);
		return result ?? [];
	}

	private async Task<ImmutableHashSet<TradingPair>> GetAvailableTradingPairsNowAsync(CancellationToken cancellationToken)
	{
		Product[]? products = await this.httpClient.GetFromJsonAsync<Product[]>("https://api.exchange.coinbase.com/products", cancellationToken).ConfigureAwait(false);
		if (products is null)
		{
			return EmptyTradingPairs;
		}

		ImmutableHashSet<TradingPair>.Builder tradingPairs = EmptyTradingPairs.ToBuilder();
		foreach (Product product in products)
		{
			Security? basis = product.BaseCurrency is null ? null : Security.WellKnown.GetValueOrDefault(product.BaseCurrency);
			Security? tradeInterest = product.QuoteCurrency is null ? null : Security.WellKnown.GetValueOrDefault(product.QuoteCurrency);

			if (basis is not null && tradeInterest is not null)
			{
				tradingPairs.Add(new TradingPair(basis, tradeInterest));
			}
		}

		return tradingPairs.ToImmutable();
	}

	private record struct Product
	{
		[JsonPropertyName("base_currency")]
		public string? BaseCurrency { get; init; }

		[JsonPropertyName("quote_currency")]
		public string? QuoteCurrency { get; init; }
	}

	[JsonConverter(typeof(ResponseItemConverter))]
	private record ResponseItem(DateTimeOffset StartTime, double Low, double High, double Open, double Close, double Volume)
	{
	}

	private class ResponseItemConverter : JsonConverter<ResponseItem>
	{
		public override ResponseItem Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType != JsonTokenType.StartArray)
			{
				throw new JsonException();
			}

			reader.Read();
			long startTime = reader.GetInt64();
			reader.Read();
			double low = reader.GetDouble();
			reader.Read();
			double high = reader.GetDouble();
			reader.Read();
			double open = reader.GetDouble();
			reader.Read();
			double close = reader.GetDouble();
			reader.Read();
			double volume = reader.GetDouble();
			reader.Read();

			if (reader.TokenType != JsonTokenType.EndArray)
			{
				throw new JsonException();
			}

			return new ResponseItem(DateTimeOffset.FromUnixTimeSeconds(startTime), low, high, open, close, volume);
		}

		public override void Write(Utf8JsonWriter writer, ResponseItem value, JsonSerializerOptions options)
		{
			writer.WriteStartArray();
			writer.WriteNumberValue(value.StartTime.ToUnixTimeSeconds());
			writer.WriteNumberValue(value.Low);
			writer.WriteNumberValue(value.High);
			writer.WriteNumberValue(value.Open);
			writer.WriteNumberValue(value.Close);
			writer.WriteNumberValue(value.Volume);
			writer.WriteEndArray();
		}
	}
}
