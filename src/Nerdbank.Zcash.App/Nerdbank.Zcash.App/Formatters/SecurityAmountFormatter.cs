// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MessagePack;
using MessagePack.Formatters;
using Nerdbank.Cryptocurrencies.Exchanges;

namespace Nerdbank.Zcash.App.Formatters;

internal class SecurityAmountFormatter : IMessagePackFormatter<SecurityAmount>
{
	internal static readonly SecurityAmountFormatter Instance = new();

	private SecurityAmountFormatter()
	{
	}

	public SecurityAmount Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
	{
		options.Security.DepthStep(ref reader);

		decimal amount = 0;
		Security? security = null;

		int length = reader.ReadArrayHeader();
		for (int i = 0; i < length; i++)
		{
			switch (i)
			{
				case 0:
					amount = options.Resolver.GetFormatterWithVerify<decimal>().Deserialize(ref reader, options);
					break;
				case 1:
					string ticker = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
					if (!Security.WellKnown.TryGetValue(ticker, out security))
					{
						throw new MessagePackSerializationException($"Unrecognized security ticker symbol: {ticker}");
					}

					break;
				default:
					reader.Skip();
					break;
			}
		}

		reader.Depth--;

		if (security is null)
		{
			throw new MessagePackSerializationException("Missing security ticker symbol.");
		}

		return new SecurityAmount(amount, security);
	}

	public void Serialize(ref MessagePackWriter writer, SecurityAmount value, MessagePackSerializerOptions options)
	{
		writer.WriteArrayHeader(2);
		options.Resolver.GetFormatterWithVerify<decimal>().Serialize(ref writer, value.Amount, options);
		options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Security.TickerSymbol, options);
	}
}
