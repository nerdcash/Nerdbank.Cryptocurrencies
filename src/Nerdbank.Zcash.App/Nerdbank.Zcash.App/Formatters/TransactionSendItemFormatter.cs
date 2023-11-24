// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MessagePack;
using MessagePack.Formatters;

namespace Nerdbank.Zcash.App.Formatters;

internal class TransactionSendItemFormatter : IMessagePackFormatter<Transaction.SendItem>
{
	internal static readonly TransactionSendItemFormatter Instance = new();

	private TransactionSendItemFormatter()
	{
	}

	public Transaction.SendItem Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
	{
		options.Security.DepthStep(ref reader);

		int length = reader.ReadArrayHeader();
		ZcashAddress? address = null;
		decimal amount = 0;
		Memo? memo = null;
		UnifiedAddress? recipientUA = null;

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
					recipientUA = options.Resolver.GetFormatterWithVerify<UnifiedAddress?>().Deserialize(ref reader, options);
					break;
				default:
					reader.Skip();
					break;
			}
		}

		if (memo is null)
		{
			throw new MessagePackSerializationException("Unsufficient data.");
		}

		reader.Depth--;

		return new Transaction.SendItem(address!, amount, memo.Value)
		{
			RecipientUA = recipientUA,
		};
	}

	public void Serialize(ref MessagePackWriter writer, Transaction.SendItem value, MessagePackSerializerOptions options)
	{
		writer.WriteArrayHeader(value.RecipientUA is null ? 3 : 4);
		options.Resolver.GetFormatterWithVerify<ZcashAddress>().Serialize(ref writer, value.ToAddress, options);
		options.Resolver.GetFormatterWithVerify<decimal>().Serialize(ref writer, value.Amount, options);
		options.Resolver.GetFormatterWithVerify<Memo>().Serialize(ref writer, value.Memo, options);
		if (value.RecipientUA is not null)
		{
			options.Resolver.GetFormatterWithVerify<UnifiedAddress?>().Serialize(ref writer, value.RecipientUA, options);
		}
	}
}
