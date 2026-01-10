// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;
using Nerdbank.Cryptocurrencies.Exchanges;

[Trait("RequiresNetwork", "true")]
public class CoinbaseTests(ITestOutputHelper logger) : HistoricalPriceTestBase(logger)
{
	private readonly Coinbase exchange = new(new HttpClient() { DefaultRequestHeaders = { { "User-Agent", "Nerdbank.Cryptocurrencies.Tests" } } })
	{
		Logger = new XunitLogger(logger),
	};

	protected override IHistoricalExchangeRateProvider Provider => this.exchange;

	[Fact]
	public async Task GetExchangeRateAsync()
	{
		DateTimeOffset when = new DateTimeOffset(2024, 10, 6, 13, 23, 5, 0, TimeSpan.Zero);
		ExchangeRate? rate = await this.exchange.GetExchangeRateAsync(UsdZec, when, this.TimeoutToken);
		this.Logger.WriteLine($"{rate}");
		Assert.NotNull(rate);
		Assert.Equal(Security.USD, rate.Value.Basis.Security);
		Assert.True(Math.Abs(rate.Value.InBasisAmount.RoundedAmount - 29.08m) < 0.20m);
	}

	[Fact]
	public async Task Hourly_MultipleTimestampsInSameSegment_SingleQuery()
	{
		// Hourly granularity: 300 candles * 1 hour = 300 hours per segment (~12.5 days)
		using RequestCountingHandler handler = new();
		Coinbase exchange = this.CreateExchangeWithCounting(handler, Coinbase.Granularity.Hourly);

		// All these timestamps fall within the same segment
		DateTimeOffset baseTime = new(2024, 10, 6, 12, 0, 0, TimeSpan.Zero);
		await exchange.GetExchangeRateAsync(UsdZec, baseTime, this.TimeoutToken);
		await exchange.GetExchangeRateAsync(UsdZec, baseTime.AddHours(5), this.TimeoutToken);
		await exchange.GetExchangeRateAsync(UsdZec, baseTime.AddHours(-3), this.TimeoutToken);
		await exchange.GetExchangeRateAsync(UsdZec, baseTime.AddDays(2), this.TimeoutToken);

		// Should have made exactly 2 queries: one for trading pairs, one for candles
		Assert.Equal(2, handler.CandleRequestCount + handler.ProductRequestCount);
		Assert.Equal(1, handler.CandleRequestCount);
		this.Logger.WriteLine($"Total requests: {handler.CandleRequestCount + handler.ProductRequestCount}, Candle requests: {handler.CandleRequestCount}");
	}

	[Fact]
	public async Task Hourly_TimestampsInDifferentSegments_MultipleQueries()
	{
		// Hourly granularity: 300 candles * 1 hour = 300 hours per segment (~12.5 days)
		using RequestCountingHandler handler = new();
		Coinbase exchange = this.CreateExchangeWithCounting(handler, Coinbase.Granularity.Hourly);

		// These timestamps are in different segments (more than 12.5 days apart)
		DateTimeOffset time1 = new(2024, 10, 6, 12, 0, 0, TimeSpan.Zero);
		DateTimeOffset time2 = time1.AddDays(15); // Different segment

		await exchange.GetExchangeRateAsync(UsdZec, time1, this.TimeoutToken);
		await exchange.GetExchangeRateAsync(UsdZec, time2, this.TimeoutToken);

		// Should have made 3 queries: one for trading pairs, two for candles (one per segment)
		Assert.Equal(3, handler.CandleRequestCount + handler.ProductRequestCount);
		Assert.Equal(2, handler.CandleRequestCount);
		this.Logger.WriteLine($"Total requests: {handler.CandleRequestCount + handler.ProductRequestCount}, Candle requests: {handler.CandleRequestCount}");
	}

	[Fact]
	public async Task Minute_SmallSegmentsCacheCorrectly()
	{
		// Minute granularity: 300 candles * 1 minute = 300 minutes (5 hours) per segment
		using RequestCountingHandler handler = new();
		Coinbase exchange = this.CreateExchangeWithCounting(handler, Coinbase.Granularity.Minute);

		// All these timestamps should be in the same 5-hour segment
		DateTimeOffset baseTime = new(2024, 10, 6, 12, 30, 0, TimeSpan.Zero);
		await exchange.GetExchangeRateAsync(UsdZec, baseTime, this.TimeoutToken);
		await exchange.GetExchangeRateAsync(UsdZec, baseTime.AddMinutes(30), this.TimeoutToken);
		await exchange.GetExchangeRateAsync(UsdZec, baseTime.AddMinutes(-45), this.TimeoutToken);

		Assert.Equal(1, handler.CandleRequestCount);
		this.Logger.WriteLine($"Minute granularity - Candle requests for same segment: {handler.CandleRequestCount}");
	}

	[Fact]
	public async Task Minute_DifferentSegments_MultipleQueries()
	{
		// Minute granularity: 300 candles * 1 minute = 300 minutes (5 hours) per segment
		using RequestCountingHandler handler = new();
		Coinbase exchange = this.CreateExchangeWithCounting(handler, Coinbase.Granularity.Minute);

		// These timestamps are in different 5-hour segments
		DateTimeOffset time1 = new(2024, 10, 6, 12, 30, 0, TimeSpan.Zero);
		DateTimeOffset time2 = time1.AddHours(6); // Different segment

		await exchange.GetExchangeRateAsync(UsdZec, time1, this.TimeoutToken);
		await exchange.GetExchangeRateAsync(UsdZec, time2, this.TimeoutToken);

		Assert.Equal(2, handler.CandleRequestCount);
		this.Logger.WriteLine($"Minute granularity - Candle requests for different segments: {handler.CandleRequestCount}");
	}

	[Fact]
	public async Task Daily_LargeSegmentsCacheCorrectly()
	{
		// Daily granularity: 300 candles * 1 day = 300 days per segment
		using RequestCountingHandler handler = new();
		Coinbase exchange = this.CreateExchangeWithCounting(handler, Coinbase.Granularity.Daily);

		// All these timestamps should be in the same 300-day segment
		// Use timestamps that are close together to avoid crossing segment boundaries
		DateTimeOffset baseTime = new(2024, 10, 6, 12, 0, 0, TimeSpan.Zero);
		await exchange.GetExchangeRateAsync(UsdZec, baseTime, this.TimeoutToken);
		await exchange.GetExchangeRateAsync(UsdZec, baseTime.AddDays(10), this.TimeoutToken);
		await exchange.GetExchangeRateAsync(UsdZec, baseTime.AddDays(20), this.TimeoutToken);
		await exchange.GetExchangeRateAsync(UsdZec, baseTime.AddDays(30), this.TimeoutToken);

		Assert.Equal(1, handler.CandleRequestCount);
		this.Logger.WriteLine($"Daily granularity - Candle requests for same segment: {handler.CandleRequestCount}");
	}

	[Fact]
	public async Task FiveMinutes_SegmentBoundaryBehavior()
	{
		// FiveMinutes granularity: 300 candles * 5 minutes = 1500 minutes (25 hours) per segment
		using RequestCountingHandler handler = new();
		Coinbase exchange = this.CreateExchangeWithCounting(handler, Coinbase.Granularity.FiveMinutes);

		// Calculate a timestamp that is well within a segment by aligning to segment boundary
		TimeSpan segmentDuration = TimeSpan.FromMinutes(5 * 300); // 25 hours
		DateTimeOffset recentTime = new(2024, 10, 1, 0, 0, 0, TimeSpan.Zero);

		// Find the segment that contains this time
		long segmentTicks = segmentDuration.Ticks;
		long timeTicks = recentTime.UtcTicks;
		long segmentStartTicks = (timeTicks / segmentTicks) * segmentTicks;
		DateTimeOffset segmentStart = new(segmentStartTicks, TimeSpan.Zero);

		// Query near the start of this segment
		await exchange.GetExchangeRateAsync(UsdZec, segmentStart.AddHours(1), this.TimeoutToken);

		// Query later in the same segment (should be cached)
		await exchange.GetExchangeRateAsync(UsdZec, segmentStart.AddHours(5), this.TimeoutToken);

		// Query even later (still within 25 hours)
		await exchange.GetExchangeRateAsync(UsdZec, segmentStart.AddHours(10), this.TimeoutToken);

		Assert.Equal(1, handler.CandleRequestCount);
		this.Logger.WriteLine($"FiveMinutes granularity - Same segment queries: {handler.CandleRequestCount}");

		// Now query a different segment (next segment)
		await exchange.GetExchangeRateAsync(UsdZec, segmentStart.Add(segmentDuration).AddHours(1), this.TimeoutToken);

		Assert.Equal(2, handler.CandleRequestCount);
		this.Logger.WriteLine($"FiveMinutes granularity - After crossing segment: {handler.CandleRequestCount}");
	}

	[Fact]
	public async Task SixHours_SegmentsCacheCorrectly()
	{
		// SixHours granularity: 300 candles * 6 hours = 1800 hours (75 days) per segment
		using RequestCountingHandler handler = new();
		Coinbase exchange = this.CreateExchangeWithCounting(handler, Coinbase.Granularity.SixHours);

		DateTimeOffset baseTime = new(2024, 10, 6, 12, 0, 0, TimeSpan.Zero);

		// Multiple queries within 75-day segment (use smaller offsets to avoid boundary issues)
		await exchange.GetExchangeRateAsync(UsdZec, baseTime, this.TimeoutToken);
		await exchange.GetExchangeRateAsync(UsdZec, baseTime.AddDays(10), this.TimeoutToken);
		await exchange.GetExchangeRateAsync(UsdZec, baseTime.AddDays(20), this.TimeoutToken);

		Assert.Equal(1, handler.CandleRequestCount);
		this.Logger.WriteLine($"SixHours granularity - Candle requests: {handler.CandleRequestCount}");
	}

	[Fact]
	public async Task CacheWorksAcrossMultipleTradingPairs()
	{
		using RequestCountingHandler handler = new();
		Coinbase exchange = this.CreateExchangeWithCounting(handler, Coinbase.Granularity.Hourly);

		DateTimeOffset when = new(2024, 10, 6, 12, 0, 0, TimeSpan.Zero);

		// Query USD-ZEC
		await exchange.GetExchangeRateAsync(UsdZec, when, this.TimeoutToken);
		Assert.Equal(1, handler.CandleRequestCount);

		// Query same time again (cached)
		await exchange.GetExchangeRateAsync(UsdZec, when, this.TimeoutToken);
		Assert.Equal(1, handler.CandleRequestCount);

		// Query reversed pair (should use same cache due to TradingPairEitherOrderEqualityComparer)
		await exchange.GetExchangeRateAsync(UsdZec.OppositeDirection, when, this.TimeoutToken);
		Assert.Equal(1, handler.CandleRequestCount);

		this.Logger.WriteLine($"Trading pair caching - Candle requests: {handler.CandleRequestCount}");
	}

	[Fact]
	public async Task SequentialQueriesAcrossSegmentBoundaries()
	{
		// Test that sequential queries that cross segment boundaries result in expected queries
		using RequestCountingHandler handler = new();
		Coinbase exchange = this.CreateExchangeWithCounting(handler, Coinbase.Granularity.FifteenMinutes);

		// FifteenMinutes: 300 candles * 15 minutes = 4500 minutes (75 hours) per segment
		TimeSpan segmentDuration = TimeSpan.FromMinutes(15 * 300); // 75 hours
		DateTimeOffset recentTime = new(2024, 10, 1, 0, 0, 0, TimeSpan.Zero);

		// Find the segment that contains this time
		long segmentTicks = segmentDuration.Ticks;
		long timeTicks = recentTime.UtcTicks;
		long segmentStartTicks = (timeTicks / segmentTicks) * segmentTicks;
		DateTimeOffset segmentStart = new(segmentStartTicks, TimeSpan.Zero);

		// Query near the start of this segment
		await exchange.GetExchangeRateAsync(UsdZec, segmentStart.AddHours(1), this.TimeoutToken);
		Assert.Equal(1, handler.CandleRequestCount);

		// Query later in the same segment (should be cached)
		await exchange.GetExchangeRateAsync(UsdZec, segmentStart.AddHours(12), this.TimeoutToken);
		Assert.Equal(1, handler.CandleRequestCount);

		// Query even later (still within 75 hours)
		await exchange.GetExchangeRateAsync(UsdZec, segmentStart.AddHours(24), this.TimeoutToken);
		Assert.Equal(1, handler.CandleRequestCount);

		// Query in the next segment
		await exchange.GetExchangeRateAsync(UsdZec, segmentStart.Add(segmentDuration).AddHours(1), this.TimeoutToken);
		Assert.Equal(2, handler.CandleRequestCount);

		this.Logger.WriteLine($"Sequential boundary crossing - Candle requests: {handler.CandleRequestCount}");
	}

	private static HttpClient CreateCountingHttpClient(RequestCountingHandler handler)
	{
		handler.InnerHandler = new HttpClientHandler();
		return new HttpClient(handler)
		{
			DefaultRequestHeaders = { { "User-Agent", "Nerdbank.Cryptocurrencies.Tests" } },
		};
	}

	private Coinbase CreateExchangeWithCounting(RequestCountingHandler handler, Coinbase.Granularity granularity)
	{
		return new Coinbase(CreateCountingHttpClient(handler), granularity)
		{
			Logger = new XunitLogger(this.Logger),
		};
	}

	private class RequestCountingHandler : DelegatingHandler
	{
		private int candleRequestCount;
		private int productRequestCount;

		public int CandleRequestCount => this.candleRequestCount;

		public int ProductRequestCount => this.productRequestCount;

		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			if (request.RequestUri?.AbsolutePath.Contains("/candles") == true)
			{
				Interlocked.Increment(ref this.candleRequestCount);
			}
			else if (request.RequestUri?.AbsolutePath == "/products")
			{
				Interlocked.Increment(ref this.productRequestCount);
			}

			return base.SendAsync(request, cancellationToken);
		}
	}

	private class XunitLogger(ITestOutputHelper output) : ILogger
	{
		public IDisposable? BeginScope<TState>(TState state)
			where TState : notnull => null;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
		{
			output.WriteLine($"[{logLevel}] {formatter(state, exception)}");
			if (exception is not null)
			{
				output.WriteLine(exception.ToString());
			}
		}
	}
}
