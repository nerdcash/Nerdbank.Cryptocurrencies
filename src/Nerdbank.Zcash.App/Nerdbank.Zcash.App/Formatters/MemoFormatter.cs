// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;
using MessagePack;
using MessagePack.Formatters;

namespace Nerdbank.Zcash.App.Formatters;

internal class MemoFormatter : IMessagePackFormatter<Memo>
{
	public static readonly MemoFormatter Instance = new();

	private MemoFormatter()
	{
	}

	public Memo Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
	{
		if (reader.TryReadNil())
		{
			return Memo.NoMemo;
		}

		if (reader.NextMessagePackType == MessagePackType.Binary)
		{
			ReadOnlySequence<byte> rawBytes = reader.ReadBytes()!.Value;
			return new Memo(rawBytes.ToArray());
		}

		string message = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
		return Memo.FromMessage(message);
	}

	public void Serialize(ref MessagePackWriter writer, Memo value, MessagePackSerializerOptions options)
	{
		switch (value.MemoFormat)
		{
			case Zip302MemoFormat.MemoFormat.NoMemo:
				writer.WriteNil();
				break;
			case Zip302MemoFormat.MemoFormat.Message:
				options.Resolver.GetFormatterWithVerify<string?>().Serialize(ref writer, value.Message, options);
				break;
			default:
				ReadOnlySpan<byte> rawBytes = value.RawBytes.TrimEnd((byte)0);
				writer.WriteBinHeader(rawBytes.Length);
				rawBytes.CopyTo(writer.GetSpan(rawBytes.Length));
				writer.Advance(rawBytes.Length);
				break;
		}
	}
}
