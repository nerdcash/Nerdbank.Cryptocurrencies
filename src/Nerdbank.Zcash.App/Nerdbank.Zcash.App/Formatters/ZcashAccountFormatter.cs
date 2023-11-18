// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MessagePack;
using MessagePack.Formatters;

namespace Nerdbank.Zcash.App.Formatters;

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

		writer.WriteArrayHeader(4);
		if (value.HDDerivation is { } derivation)
		{
			writer.WriteNil();
			options.Resolver.GetFormatterWithVerify<Zip32HDWallet?>().Serialize(ref writer, derivation.Wallet, options);
			writer.Write(derivation.AccountIndex);
		}
		else
		{
			string v = value.FullViewing?.UnifiedKey.TextEncoding ?? value.IncomingViewing.UnifiedKey.TextEncoding;
			options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, v, options);
			writer.WriteNil();
			writer.WriteNil();
		}

		options.Resolver.GetFormatterWithVerify<ulong?>().Serialize(ref writer, value.BirthdayHeight, options);
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
		ulong? birthdayHeight = null;

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
					if (!reader.TryReadNil())
					{
						if (zip32 is null)
						{
							throw new MessagePackSerializationException();
						}

						accountIndex = reader.ReadUInt32();
					}

					break;
				case 3:
					birthdayHeight = options.Resolver.GetFormatterWithVerify<ulong?>().Deserialize(ref reader, options);
					break;
				default:
					reader.Skip();
					break;
			}
		}

		reader.Depth--;

		ZcashAccount result = zip32 is not null
			? new ZcashAccount(zip32, accountIndex ?? throw new MessagePackSerializationException("Missing account index."))
			: new ZcashAccount(UnifiedViewingKey.Decode(uvk ?? throw new MessagePackSerializationException("Missing UVK.")));
		result.BirthdayHeight = birthdayHeight;
		return result;
	}
}
