// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MessagePack;
using MessagePack.Formatters;

namespace Nerdbank.Zcash.App.Formatters;

internal class ZcashAddressFormatter : IMessagePackFormatter<ZcashAddress?>
{
	internal static readonly ZcashAddressFormatter Instance = new();

	private ZcashAddressFormatter()
	{
	}

	public ZcashAddress? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
	{
		if (reader.TryReadNil())
		{
			return null;
		}

		string address = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
		return ZcashAddress.Decode(address);
	}

	public void Serialize(ref MessagePackWriter writer, ZcashAddress? value, MessagePackSerializerOptions options)
	{
		if (value is null)
		{
			writer.WriteNil();
			return;
		}

		options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Address, options);
	}
}
