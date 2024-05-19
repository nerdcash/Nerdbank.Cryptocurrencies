// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MessagePack;
using MessagePack.Formatters;
using Nerdbank.Cryptocurrencies;
using Nerdbank.Cryptocurrencies.Exchanges;

namespace Nerdbank.Zcash.App.Formatters;

internal class ExchangeRateFormatter : IMessagePackFormatter<ExchangeRate>
{
	internal static readonly ExchangeRateFormatter Instance = new();

	private ExchangeRateFormatter()
	{
	}

	public ExchangeRate Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
	{
		options.Security.DepthStep(ref reader);

		int length = reader.ReadArrayHeader();
		if (length != 2)
		{
			throw new MessagePackSerializationException();
		}

		SecurityAmount basis = options.Resolver.GetFormatterWithVerify<SecurityAmount>().Deserialize(ref reader, options);
		SecurityAmount tradeInterest = options.Resolver.GetFormatterWithVerify<SecurityAmount>().Deserialize(ref reader, options);

		reader.Depth--;

		return new ExchangeRate(basis, tradeInterest);
	}

	public void Serialize(ref MessagePackWriter writer, ExchangeRate value, MessagePackSerializerOptions options)
	{
		writer.WriteArrayHeader(2);
		options.Resolver.GetFormatterWithVerify<SecurityAmount>().Serialize(ref writer, value.Basis, options);
		options.Resolver.GetFormatterWithVerify<SecurityAmount>().Serialize(ref writer, value.TradeInterest, options);
	}
}
