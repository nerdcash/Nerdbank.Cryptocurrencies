// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;
using MessagePack;
using MessagePack.Formatters;

namespace Nerdbank.Zcash.App.Formatters;

internal class TxIdFormatter : IMessagePackFormatter<TxId>
{
	internal static readonly TxIdFormatter Instance = new();

	private TxIdFormatter()
	{
	}

	public TxId Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
	{
		ReadOnlySequence<byte>? txidBytes = reader.ReadBytes();
		if (txidBytes is null)
		{
			throw new MessagePackSerializationException("Unexpected nil value for TxId");
		}

		return new TxId(txidBytes.Value.ToArray());
	}

	public void Serialize(ref MessagePackWriter writer, TxId value, MessagePackSerializerOptions options)
	{
		ReadOnlySpan<byte> txid = value[..];
		writer.WriteBinHeader(txid.Length);
		txid.CopyTo(writer.GetSpan(txid.Length));
		writer.Advance(txid.Length);
	}
}
