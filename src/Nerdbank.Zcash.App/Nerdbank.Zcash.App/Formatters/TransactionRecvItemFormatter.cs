// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MessagePack;
using MessagePack.Formatters;

namespace Nerdbank.Zcash.App.Formatters;

internal class TransactionRecvItemFormatter : IMessagePackFormatter<Transaction.RecvItem>
{
	internal static readonly TransactionRecvItemFormatter Instance = new();

	private TransactionRecvItemFormatter()
	{
	}

	public Transaction.RecvItem Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
	{
		options.Security.DepthStep(ref reader);

		int length = reader.ReadArrayHeader();
		ZcashAddress? address = null;
		decimal amount = 0;
		Memo? memo = null;
		bool? isChange = null;

		for (int i = 0; i < length; i++)
		{
			switch (i)
			{
				case 0:
					address = options.Resolver.GetFormatterWithVerify<ZcashAddress>().Deserialize(ref reader, options);
					break;
				case 1:
					amount = options.Resolver.GetFormatterWithVerify<decimal>().Deserialize(ref reader, options);
					break;
				case 2:
					memo = options.Resolver.GetFormatterWithVerify<Memo>().Deserialize(ref reader, options);
					break;
				case 3:
					isChange = reader.ReadBoolean();
					break;
				default:
					reader.Skip();
					break;
			}
		}

		if (isChange is null)
		{
			throw new MessagePackSerializationException("Unsufficient data.");
		}

		reader.Depth--;

		return new Transaction.RecvItem(address!, amount, memo!.Value, isChange.Value);
	}

	public void Serialize(ref MessagePackWriter writer, Transaction.RecvItem value, MessagePackSerializerOptions options)
	{
		writer.WriteArrayHeader(4);
		options.Resolver.GetFormatterWithVerify<ZcashAddress>().Serialize(ref writer, value.ToAddress, options);
		options.Resolver.GetFormatterWithVerify<decimal>().Serialize(ref writer, value.Amount, options);
		options.Resolver.GetFormatterWithVerify<Memo>().Serialize(ref writer, value.Memo, options);
		writer.Write(value.IsChange);
	}
}
