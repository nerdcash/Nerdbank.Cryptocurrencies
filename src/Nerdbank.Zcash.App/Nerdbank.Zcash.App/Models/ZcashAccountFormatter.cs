// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MessagePack;
using MessagePack.Formatters;

namespace Nerdbank.Zcash.App.Models;

internal class ZcashAccountFormatter : IMessagePackFormatter<ZcashAccount?>
{
	internal static readonly ZcashAccountFormatter Instance = new();

	private ZcashAccountFormatter()
	{
	}

	public void Serialize(ref MessagePackWriter writer, ZcashAccount? value, MessagePackSerializerOptions options)
	{
		if (value is null)
		{
			writer.WriteNil();
			return;
		}

		if (value.HDDerivation is { } derivation)
		{
			writer.WriteArrayHeader(3);
			writer.WriteNil();
			options.Resolver.GetFormatterWithVerify<Zip32HDWallet?>().Serialize(ref writer, derivation.Wallet, options);
			writer.Write(derivation.AccountIndex);
		}
		else
		{
			writer.WriteArrayHeader(1);
			string v = value.FullViewing?.UnifiedKey.TextEncoding ?? value.IncomingViewing.UnifiedKey.TextEncoding;
			options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, v, options);
		}
	}

	public ZcashAccount? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
	{
		if (reader.TryReadNil())
		{
			return null;
		}

		string? uvk = null;
		Zip32HDWallet? zip32 = null;
		uint? accountIndex = null;

		options.Security.DepthStep(ref reader);
		int length = reader.ReadArrayHeader();
		for (int i = 0; i < length; i++)
		{
			switch (i)
			{
				case 0:
					uvk = options.Resolver.GetFormatterWithVerify<string?>().Deserialize(ref reader, options);
					break;
				case 1:
					zip32 = options.Resolver.GetFormatterWithVerify<Zip32HDWallet>().Deserialize(ref reader, options);
					break;
				case 2:
					if (zip32 is null)
					{
						throw new MessagePackSerializationException();
					}

					if (!reader.TryReadNil())
					{
						accountIndex = reader.ReadUInt32();
					}

					break;

				default:
					reader.Skip();
					break;
			}
		}

		reader.Depth--;

		return zip32 is not null
			? new ZcashAccount(zip32, accountIndex ?? throw new MessagePackSerializationException("Missing account index."))
			: new ZcashAccount(UnifiedViewingKey.Decode(uvk ?? throw new MessagePackSerializationException("Missing UVK.")));
	}
}
