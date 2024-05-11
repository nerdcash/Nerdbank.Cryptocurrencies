// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MessagePack;
using MessagePack.Formatters;
using Nerdbank.Cryptocurrencies;

namespace Nerdbank.Zcash.App.Formatters;

internal class SecurityFormatter : IMessagePackFormatter<Security>
{
	internal static readonly SecurityFormatter Instance = new();

	private SecurityFormatter()
	{
	}

	public Security Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
	{
		string ticker = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
		if (!Security.WellKnown.TryGetValue(ticker, out Security? security))
		{
			throw new MessagePackSerializationException($"Unrecognized security ticker symbol: {ticker}");
		}

		return security;
	}

	public void Serialize(ref MessagePackWriter writer, Security value, MessagePackSerializerOptions options)
	{
		options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.TickerSymbol, options);
	}
}
