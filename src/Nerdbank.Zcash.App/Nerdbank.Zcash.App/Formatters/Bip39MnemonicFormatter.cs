// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MessagePack;
using MessagePack.Formatters;

namespace Nerdbank.Zcash.App.Formatters;

internal class Bip39MnemonicFormatter : IMessagePackFormatter<Bip39Mnemonic>
{
	internal static readonly Bip39MnemonicFormatter Instance = new();

	private Bip39MnemonicFormatter()
	{
	}

	public void Serialize(ref MessagePackWriter writer, Bip39Mnemonic value, MessagePackSerializerOptions options)
	{
		writer.WriteArrayHeader(value.Password.Length > 0 ? 2 : 1);
		options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.SeedPhrase, options);
		if (value.Password.Length > 0)
		{
			options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Password.ToString(), options);
		}
	}

	public Bip39Mnemonic Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
	{
		string? seedPhrase = null;
		string? password = null;

		int length = reader.ReadArrayHeader();
		for (int i = 0; i < length; i++)
		{
			switch (i)
			{
				case 0:
					seedPhrase = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
					break;
				case 1:
					password = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
					break;
				default:
					reader.Skip();
					break;
			}
		}

		if (seedPhrase is null)
		{
			throw new MessagePackSerializationException("Missing required data.");
		}

		return Bip39Mnemonic.Parse(seedPhrase, password);
	}
}
