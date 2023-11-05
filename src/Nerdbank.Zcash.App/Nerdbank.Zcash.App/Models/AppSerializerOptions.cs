// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;
using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;

namespace Nerdbank.Zcash.App.Models;

internal class AppSerializerOptions : MessagePackSerializerOptions
{
	private static readonly IFormatterResolver ShareableResolver = CompositeResolver.Create(
		new IMessagePackFormatter[]
		{
			Zip32HDWalletFormatter.Instance,
		},
		new IFormatterResolver[]
		{
			StandardResolverAllowPrivate.Instance,
		});

	internal AppSerializerOptions()
		: base(Standard.WithResolver(new DedupingResolver(ShareableResolver)))
	{
	}

	private class DedupingResolver : IFormatterResolver
	{
		private const sbyte ReferenceExtensionTypeCode = 1;
		private readonly IFormatterResolver inner;
		private readonly Dictionary<object, int> serializedObjects = new();
		private readonly List<object?> deserializedObjects = new();
		private readonly Dictionary<Type, IMessagePackFormatter> dedupingFormatters = new();
		private int serializingObjectCounter;

		internal DedupingResolver(IFormatterResolver inner)
		{
			this.inner = inner;
		}

		public IMessagePackFormatter<T>? GetFormatter<T>()
		{
			if (!typeof(T).IsValueType)
			{
				return this.GetDedupingFormatter<T>();
			}

			return this.inner.GetFormatter<T>();
		}

		private IMessagePackFormatter<T>? GetDedupingFormatter<T>()
		{
			if (!this.dedupingFormatters.TryGetValue(typeof(T), out IMessagePackFormatter? formatter))
			{
				formatter = new DedupingFormatter<T>(this);
				this.dedupingFormatters.Add(typeof(T), formatter);
			}

			return (IMessagePackFormatter<T>)formatter;
		}

		private class DedupingFormatter<T> : IMessagePackFormatter<T>
		{
			private readonly DedupingResolver owner;

			internal DedupingFormatter(DedupingResolver owner)
			{
				this.owner = owner;
			}

			public T Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
			{
				if (!typeof(T).IsValueType && reader.TryReadNil())
				{
					return default!;
				}

				if (reader.NextMessagePackType == MessagePackType.Extension)
				{
					MessagePackReader provisionaryReader = reader.CreatePeekReader();
					ExtensionHeader extensionHeader = provisionaryReader.ReadExtensionFormatHeader();
					if (extensionHeader.TypeCode == ReferenceExtensionTypeCode && extensionHeader.Length == 4)
					{
						ReadOnlySequence<byte> idBytes = provisionaryReader.ReadRaw(4);
						int id;
						if (idBytes.IsSingleSegment)
						{
							id = BitConverter.ToInt32(idBytes.FirstSpan);
						}
						else
						{
							Span<byte> idBytesSpan = stackalloc byte[4];
							idBytes.CopyTo(idBytesSpan);
							id = BitConverter.ToInt32(idBytesSpan);
						}

						reader = provisionaryReader;
						return (T)(this.owner.deserializedObjects[id] ?? throw new MessagePackSerializationException("Unexpected null element in shared object array. Dependency cycle?"));
					}
				}

				// Reserve our position in the array.
				int reservation = this.owner.deserializedObjects.Count;
				this.owner.deserializedObjects.Add(null);
				T value = this.owner.inner.GetFormatterWithVerify<T>().Deserialize(ref reader, options);
				this.owner.deserializedObjects[reservation] = value;
				return value;
			}

			public void Serialize(ref MessagePackWriter writer, T value, MessagePackSerializerOptions options)
			{
				if (value is null)
				{
					writer.WriteNil();
					return;
				}

				if (this.owner.serializedObjects.TryGetValue(value, out int referenceId))
				{
					// This object has already been written. Skip it this time.
					writer.WriteExtensionFormatHeader(new ExtensionHeader(ReferenceExtensionTypeCode, 4));
					BitConverter.TryWriteBytes(writer.GetSpan(4), referenceId);
					writer.Advance(4);
					return;
				}
				else
				{
					int reservation = this.owner.serializingObjectCounter++;
					this.owner.inner.GetFormatterWithVerify<T>().Serialize(ref writer, value, options);
					this.owner.serializedObjects.Add(value, reservation);
				}
			}
		}
	}
}
