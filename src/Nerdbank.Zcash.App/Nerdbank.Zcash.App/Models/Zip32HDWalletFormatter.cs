// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;
using MessagePack;
using MessagePack.Formatters;

namespace Nerdbank.Zcash.App.Models;

internal class Zip32HDWalletFormatter : IMessagePackFormatter<Zip32HDWallet>
{
	internal static readonly Zip32HDWalletFormatter Instance = new();

	private Zip32HDWalletFormatter()
	{
	}

	public Zip32HDWallet Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
	{
		options.Security.DepthStep(ref reader);

		int length = reader.ReadArrayHeader();
		if (length < 1)
		{
			throw new MessagePackSerializationException("Invalid Zip32HDWallet data.");
		}

		ZcashNetwork network = options.Resolver.GetFormatterWithVerify<ZcashNetwork>().Deserialize(ref reader, options);
		Zip32HDWallet wallet;

		if (reader.NextMessagePackType == MessagePackType.String)
		{
			string seedPhrase = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
			string? password = null;
			if (length > 2)
			{
				password = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
			}

			wallet = new Zip32HDWallet(Bip39Mnemonic.Parse(seedPhrase, password), network);
		}
		else
		{
			wallet = new Zip32HDWallet(reader.ReadBytes()!.Value.ToArray(), network);
		}

		reader.Depth--;
		return wallet;
	}

	public void Serialize(ref MessagePackWriter writer, Zip32HDWallet value, MessagePackSerializerOptions options)
	{
		writer.WriteArrayHeader(value.Mnemonic is null ? 2 : value.Mnemonic.Password.IsEmpty ? 2 : 3);
		options.Resolver.GetFormatterWithVerify<ZcashNetwork>().Serialize(ref writer, value.Network, options);
		if (value.Mnemonic is null)
		{
			writer.Write(value.Seed.Span);
		}
		else
		{
			options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Mnemonic.SeedPhrase, options);
			if (!value.Mnemonic.Password.IsEmpty)
			{
				options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Mnemonic.Password.ToString(), options);
			}
		}
	}
}
