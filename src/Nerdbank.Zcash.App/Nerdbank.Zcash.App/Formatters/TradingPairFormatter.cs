// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MessagePack;
using MessagePack.Formatters;
using Nerdbank.Cryptocurrencies;
using Nerdbank.Cryptocurrencies.Exchanges;

namespace Nerdbank.Zcash.App.Formatters;

internal class TradingPairFormatter : IMessagePackFormatter<TradingPair>
{
	internal static readonly TradingPairFormatter Instance = new();

	private TradingPairFormatter()
	{
	}

	public TradingPair Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
	{
		options.Security.DepthStep(ref reader);

		int length = reader.ReadArrayHeader();
		if (length != 2)
		{
			throw new MessagePackSerializationException();
		}

		Security basis = options.Resolver.GetFormatterWithVerify<Security>().Deserialize(ref reader, options);
		Security tradeInterest = options.Resolver.GetFormatterWithVerify<Security>().Deserialize(ref reader, options);

		reader.Depth--;

		return new TradingPair(basis, tradeInterest);
	}

	public void Serialize(ref MessagePackWriter writer, TradingPair value, MessagePackSerializerOptions options)
	{
		writer.WriteArrayHeader(2);
		options.Resolver.GetFormatterWithVerify<Security>().Serialize(ref writer, value.Basis, options);
		options.Resolver.GetFormatterWithVerify<Security>().Serialize(ref writer, value.TradeInterest, options);
	}
}
