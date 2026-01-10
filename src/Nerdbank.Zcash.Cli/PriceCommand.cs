// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using System.CommandLine.IO;
using Nerdbank.Cryptocurrencies;
using Nerdbank.Cryptocurrencies.Exchanges;

namespace Nerdbank.Zcash.Cli;

internal class PriceCommand
{
	private static readonly Option<DateTimeOffset> TimestampOption = new(["--timestamp", "-t"], () => DateTimeOffset.UtcNow, Strings.PriceTimestampOptionDescription);

	private static readonly Option<string> CurrencyOption = new(["--currency", "-c"], () => "USD", Strings.PriceCurrencyOptionDescription);

	internal required IConsole Console { get; init; }

	internal required DateTimeOffset Timestamp { get; init; }

	internal required string Currency { get; init; }

	internal static Command BuildCommand()
	{
		Command command = new("price", Strings.PriceCommandDescription);
		command.AddOption(TimestampOption);
		command.AddOption(CurrencyOption);

		command.SetHandler(async ctxt =>
		{
			ctxt.ExitCode = await new PriceCommand
			{
				Console = ctxt.Console,
				Timestamp = ctxt.ParseResult.GetValueForOption(TimestampOption),
				Currency = ctxt.ParseResult.GetValueForOption(CurrencyOption)!,
			}.ExecuteAsync(ctxt.GetCancellationToken());
		});

		return command;
	}

	internal async Task<int> ExecuteAsync(CancellationToken cancellationToken)
	{
		if (!Security.WellKnown.TryGetValue(this.Currency, out Security? currency))
		{
			this.Console.Error.WriteLine(Strings.FormatPriceUnknownCurrency(this.Currency));
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
				this.Console.Error.WriteLine(Strings.FormatPriceNotAvailable(this.Timestamp));
				return 1;
			}

			this.Console.WriteLine(Strings.FormatPriceResult(rate.Value.Basis, rate.Value.TradeInterest, this.Timestamp, ((IHistoricalExchangeRateProvider)coinbase).Resolution));
			return 0;
		}
		catch (Exception ex)
		{
			this.Console.Error.WriteLine(Strings.FormatPriceError(ex.Message));
			return 1;
		}
	}
}
