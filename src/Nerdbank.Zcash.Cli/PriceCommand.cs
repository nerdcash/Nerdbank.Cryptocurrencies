// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using Nerdbank.Cryptocurrencies;
using Nerdbank.Cryptocurrencies.Exchanges;

namespace Nerdbank.Zcash.Cli;

internal class PriceCommand
{
	private static readonly Option<DateTimeOffset?> TimestampOption = new("--timestamp", "-t")
	{
		Description = Strings.PriceTimestampOptionDescription,
	};

	private static readonly Option<string?> CurrencyOption = new("--currency", "-c")
	{
		Description = Strings.PriceCurrencyOptionDescription,
	};

	internal required DateTimeOffset Timestamp { get; init; }

	internal required string Currency { get; init; }

	internal static Command BuildCommand()
	{
		Command command = new("price", Strings.PriceCommandDescription)
		{
			TimestampOption,
			CurrencyOption,
		};

		command.SetAction(async (parseResult, cancellationToken) =>
		{
			DateTimeOffset timestamp = parseResult.GetValue(TimestampOption) ?? DateTimeOffset.UtcNow;
			string currency = parseResult.GetValue(CurrencyOption) ?? "USD";

			return await new PriceCommand
			{
				Timestamp = timestamp,
				Currency = currency,
			}.ExecuteAsync(cancellationToken);
		});

		return command;
	}

	internal async Task<int> ExecuteAsync(CancellationToken cancellationToken)
	{
		if (!Security.WellKnown.TryGetValue(this.Currency, out Security? currency))
		{
			await Console.Error.WriteLineAsync(Strings.FormatPriceUnknownCurrency(this.Currency));
			return 1;
		}

		using HttpClient httpClient = new()
		{
			DefaultRequestHeaders =
			{
				{ "User-Agent", "Nerdbank.Zcash.Cli" },
			},
		};
		Coinbase coinbase = new(httpClient, Coinbase.Granularity.Minute);

		TradingPair tradingPair = new(Security.ZEC, currency);

		try
		{
			ExchangeRate? rate = await coinbase.GetExchangeRateAsync(tradingPair, this.Timestamp, cancellationToken);

			if (rate is null)
			{
				await Console.Error.WriteLineAsync(Strings.FormatPriceNotAvailable(this.Timestamp));
				return 1;
			}

			Console.WriteLine(Strings.FormatPriceResult(rate.Value.Basis, rate.Value.TradeInterest, this.Timestamp, ((IHistoricalExchangeRateProvider)coinbase).Resolution));
			return 0;
		}
		catch (Exception ex)
		{
			await Console.Error.WriteLineAsync(Strings.FormatPriceError(ex.Message));
			return 1;
		}
	}
}
