// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.VisualStudio.Threading;

namespace Nerdbank.Cryptocurrencies.Exchanges;

/// <summary>
/// A Binance.US provider of exchange rates.
/// </summary>
public class BinanceUSExchange : IExchangeRateProvider
{
	private const string WebApiBaseUri = "https://api.binance.us/api/v3";
	private readonly HttpClient httpClient;
	private AsyncLazy<(ImmutableDictionary<TradingPair, string> SymbolTable, ImmutableHashSet<TradingPair> TradingPairs)> tradingPairLookup;
	private (ImmutableDictionary<string, decimal> Prices, DateTimeOffset AsOf)? prices;

	/// <summary>
	/// Initializes a new instance of the <see cref="BinanceUSExchange"/> class.
	/// </summary>
	/// <param name="httpClient">The HTTP client to use.</param>
	public BinanceUSExchange(HttpClient httpClient)
	{
		this.httpClient = httpClient;
		this.tradingPairLookup = new(() => this.GetTradingPairLookupTableAsync(CancellationToken.None), null);
	}

	/// <summary>
	/// Gets the time that prices were last refreshed.
	/// </summary>
	public DateTimeOffset? PricesAsOf => this.prices?.AsOf;

	/// <inheritdoc/>
	public async ValueTask<IReadOnlySet<TradingPair>> GetAvailableTradingPairsAsync(CancellationToken cancellationToken)
	{
		(_, ImmutableHashSet<TradingPair> tradingPairs) = await this.tradingPairLookup.GetValueAsync(cancellationToken).ConfigureAwait(false);
		return tradingPairs;
	}

	/// <inheritdoc/>
	public async ValueTask<ExchangeRate> GetExchangeRateAsync(TradingPair tradingPair, CancellationToken cancellationToken)
	{
		TradingPair exchangeTradingPair;
		(ImmutableDictionary<TradingPair, string> tradingPairs, _) = await this.tradingPairLookup.GetValueAsync(cancellationToken).ConfigureAwait(false);
		if (tradingPairs.TryGetValue(tradingPair, out string? symbolName))
		{
			exchangeTradingPair = tradingPair;
		}
		else if (tradingPairs.TryGetValue(tradingPair.OppositeDirection, out symbolName))
		{
			exchangeTradingPair = tradingPair.OppositeDirection;
		}
		else
		{
			throw new NotSupportedException(Strings.TradingPairNotSupported);
		}

		if (this.prices is null)
		{
			await this.RefreshPricesAsync(cancellationToken).ConfigureAwait(false);
		}

		ImmutableDictionary<string, decimal> prices = await this.GetPricesAsync(cancellationToken).ConfigureAwait(false);
		if (!prices.TryGetValue(symbolName, out decimal price))
		{
			throw new NotSupportedException(Strings.TradingPairNotSupported);
		}

		ExchangeRate rate = new(exchangeTradingPair.Basis.Amount(price), exchangeTradingPair.TradeInterest.Amount(1));
		return tradingPair == exchangeTradingPair ? rate : rate.OppositeDirection;
	}

	/// <summary>
	/// Fetches the latest prices from the exchange.
	/// </summary>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>A task that tracks the async refresh.</returns>
	/// <exception cref="JsonException">Thrown if the exchange does not return data in the expected format.</exception>
	/// <remarks>
	/// The resulting prices are cached locally and reused by other APIs until the next refresh.
	/// </remarks>
	public async Task RefreshPricesAsync(CancellationToken cancellationToken)
	{
		ImmutableDictionary<string, decimal>.Builder prices = ImmutableDictionary.CreateBuilder<string, decimal>();

		using Stream responseStream = await this.httpClient.GetStreamAsync("https://api.binance.us/api/v3/ticker/price", cancellationToken).ConfigureAwait(false);
		JsonDocument json = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken).ConfigureAwait(false);
		foreach (JsonElement symbolAndPrice in json.RootElement.EnumerateArray())
		{
			string candidateSymbol = symbolAndPrice.GetProperty("symbol").GetString() ?? throw new JsonException("symbol property missing.");
			prices[candidateSymbol] = decimal.Parse(symbolAndPrice.GetProperty("price").GetString()!);
		}

		this.prices = (prices.ToImmutable(), DateTimeOffset.UtcNow);
	}

	private async Task<ImmutableDictionary<string, decimal>> GetPricesAsync(CancellationToken cancellationToken)
	{
		if (this.prices is null)
		{
			await this.RefreshPricesAsync(cancellationToken).ConfigureAwait(false);
			Assumes.NotNull(this.prices);
		}

		return this.prices.Value.Prices;
	}

	private async Task<(ImmutableDictionary<TradingPair, string> SymbolTable, ImmutableHashSet<TradingPair> TradingPairs)> GetTradingPairLookupTableAsync(CancellationToken cancellationToken)
	{
		ImmutableDictionary<TradingPair, string>.Builder result = ImmutableDictionary.CreateBuilder<TradingPair, string>();

		using Stream responseStream = await this.httpClient.GetStreamAsync(WebApiBaseUri + "/exchangeInfo", cancellationToken).ConfigureAwait(false);
		using JsonDocument json = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken).ConfigureAwait(false);
		foreach (JsonElement symbol in json.RootElement.GetProperty("symbols").EnumerateArray())
		{
			// https://github.com/binance-us/binance-official-api-docs/blob/master/rest-api.md#terminology
			// base asset refers to the asset that is the quantity of a symbol. For the symbol BTCUSDT, BTC would be the base asset.
			// quote asset refers to the asset that is the price of a symbol. For the symbol BTCUSDT, USDT would be the quote asset.
			string symbolPairName = symbol.GetProperty("symbol"u8).GetString() ?? throw new JsonException();

			Security baseAsset = LookupOrCreate(symbol, "baseAsset"u8);
			Security quoteAsset = LookupOrCreate(symbol, "quoteAsset"u8);

			if (quoteAsset.TickerSymbol == "USD")
			{
				// Binance.US does not support trading pairs with USD as the base asset,
				// but it reports the prices as of when it dropped such support, which means
				// we can report very stale prices if we were willing to report them.
				continue;
			}

			TradingPair tradingPair = new(quoteAsset, baseAsset);
			result.Add(tradingPair, symbolPairName);

			static Security LookupOrCreate(JsonElement symbol, ReadOnlySpan<byte> asset)
			{
				string ticker = symbol.GetProperty(asset).GetString() ?? throw new JsonException();
				if (Security.WellKnown.TryGetValue(ticker, out Security? security))
				{
					return security;
				}

				ReadOnlySpan<byte> precisionSuffix = "Precision"u8;
				Span<byte> precisionPropertyName = stackalloc byte[asset.Length + precisionSuffix.Length];
				asset.CopyTo(precisionPropertyName);
				precisionSuffix.CopyTo(precisionPropertyName[asset.Length..]);
				security = new(ticker, Precision: symbol.GetProperty(precisionPropertyName).GetInt32());
				return security;
			}
		}

		ImmutableHashSet<TradingPair> set = ImmutableHashSet.CreateRange(TradingPairEitherOrderEqualityComparer.Instance, result.Keys);

		return (result.ToImmutable(), set);
	}
}
