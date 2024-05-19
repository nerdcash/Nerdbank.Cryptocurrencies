// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MessagePack;
using MessagePack.Formatters;

namespace Nerdbank.Zcash.App.Formatters;

internal class UnifiedAddressFormatter : IMessagePackFormatter<UnifiedAddress?>
{
	internal static readonly UnifiedAddressFormatter Instance = new();

	private UnifiedAddressFormatter()
	{
	}

	public UnifiedAddress? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
	{
		if (reader.TryReadNil())
		{
			return null;
		}

		string address = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
		return (UnifiedAddress)ZcashAddress.Decode(address);
	}

	public void Serialize(ref MessagePackWriter writer, UnifiedAddress? value, MessagePackSerializerOptions options)
	{
		if (value is null)
		{
			writer.WriteNil();
			return;
		}

		options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Address, options);
	}
}
