// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace Nerdbank.Zcash;

/// <summary>
/// Utility methods for unified address and viewing key encoding/decoding.
/// </summary>
internal static class UnifiedEncoding
{
	/// <summary>
	/// The length of the padding in a unified address.
	/// </summary>
	internal const int PaddingLength = 16;

	/// <summary>
	/// The shortest allowed length of the input to the <see cref="F4Jumble"/> function.
	/// </summary>
	internal const int MinF4JumbleInputLength = 48;

	/// <summary>
	/// The longest allowed length of the input to the <see cref="F4Jumble"/> function.
	/// </summary>
	internal const int MaxF4JumbleInputLength = 4194368;

	private const int F4OutputLength = 64; // known in the spec as ℒᵢ

	private static ReadOnlySpan<byte> StartingHPersonalization => new byte[] { 85, 65, 95, 70, 52, 74, 117, 109, 98, 108, 101, 95, 72, 0, 0, 0 };

	private static ReadOnlySpan<byte> StartingGPersonalization => new byte[] { 85, 65, 95, 70, 52, 74, 117, 109, 98, 108, 101, 95, 71, 0, 0, 0 };

	/// <summary>
	/// Creates a unified address or viewing key with the given set of elements.
	/// </summary>
	/// <param name="humanReadablePart">The human readable part for the string.</param>
	/// <param name="elements">The elements to include.</param>
	/// <param name="metadata">The metadata to encode.</param>
	/// <returns>The unified encoding.</returns>
	internal static string Encode(string humanReadablePart, IEnumerable<IUnifiedEncodingElement> elements, UnifiedEncodingMetadata metadata)
	{
		Requires.NotNull(elements);

		SortedDictionary<byte, ArraySegment<byte>> encodingByTypeCode = new();
		byte[]? unifiedBytesSharedArray = null;
		try
		{
			int totalLength = PaddingLength;

			// Add core elements.
			foreach (IUnifiedEncodingElement element in elements.Concat(metadata.GetElements()))
			{
				byte typeCode = element.UnifiedTypeCode;
				byte[] buffer = ArrayPool<byte>.Shared.Rent(element.UnifiedDataLength);
				int length = element.WriteUnifiedData(buffer);
				if (!encodingByTypeCode.TryAdd(typeCode, new ArraySegment<byte>(buffer, 0, length)))
				{
					throw new ArgumentException($"Only one element per type code is allowed, but two with typecode {typeCode} were included.", nameof(elements));
				}

				totalLength += 1; // type code
				totalLength += CompactSize.GetEncodedLength((ulong)length);
				totalLength += length;
			}

			Requires.Argument(totalLength > PaddingLength, nameof(elements), "At least one element is required, but the sequence was empty.");
			unifiedBytesSharedArray = ArrayPool<byte>.Shared.Rent(totalLength);
			Span<byte> unifiedBytes = unifiedBytesSharedArray[..totalLength];
			int bytesWritten = 0;
			foreach (KeyValuePair<byte, ArraySegment<byte>> typeCodeAndKey in encodingByTypeCode)
			{
				unifiedBytes[bytesWritten++] = typeCodeAndKey.Key;
				bytesWritten += CompactSize.Encode((ulong)typeCodeAndKey.Value.Count, unifiedBytes[bytesWritten..]);
				ReadOnlySpan<byte> elementEncoding = typeCodeAndKey.Value.AsSpan();
				bytesWritten += elementEncoding.CopyToRetLength(unifiedBytes[bytesWritten..]);
			}

			bytesWritten += InitializePadding(humanReadablePart, unifiedBytes.Slice(bytesWritten, PaddingLength));

			Assumes.True(bytesWritten == totalLength);

			F4Jumble(unifiedBytes[..bytesWritten]);

			Span<char> unifiedChars = stackalloc char[Bech32.GetEncodedLength(humanReadablePart.Length, unifiedBytes.Length)];
			int finalLength = Bech32.Bech32m.Encode(humanReadablePart, unifiedBytes, unifiedChars);
			Assumes.True(unifiedChars.Length == finalLength);

			return unifiedChars.ToString();
		}
		finally
		{
			if (unifiedBytesSharedArray is not null)
			{
				ArrayPool<byte>.Shared.Return(unifiedBytesSharedArray);
			}

			foreach (KeyValuePair<byte, ArraySegment<byte>> el in encodingByTypeCode)
			{
				ArrayPool<byte>.Shared.Return(el.Value.Array!);
			}
		}
	}

	/// <summary>
	/// Decodes a unified string into its human readable part and various elements.
	/// </summary>
	/// <param name="encoding">The unified address or viewing key.</param>
	/// <param name="humanReadablePart">Receives the human readable part of the unified encoding, if successful.</param>
	/// <param name="elements">Receives the elements that were embedded in the unified encoding, if successful.</param>
	/// <param name="errorCode">Receives the error code, if unsuccessful.</param>
	/// <param name="errorMessage">Receives the error message, if unsuccessful.</param>
	/// <returns>A value indicating whether decoding was successful.</returns>
	internal static bool TryDecode(ReadOnlySpan<char> encoding, [NotNullWhen(true)] out string? humanReadablePart, [NotNullWhen(true)] out IReadOnlyList<UnknownElement>? elements, [NotNullWhen(false)] out DecodeError? errorCode, [NotNullWhen(false)] out string? errorMessage)
	{
		humanReadablePart = null;
		elements = null;
		(int Tag, int Data)? length = Bech32.GetDecodedLength(encoding);
		if (length is null)
		{
			errorCode = DecodeError.UnrecognizedAddressType;
			errorMessage = Strings.UnrecognizedAddress;
			return false;
		}

		Span<char> humanReadablePartChars = stackalloc char[length.Value.Tag];
		Memory<byte> data = new byte[length.Value.Data];
		if (!Bech32.Bech32m.TryDecode(encoding, humanReadablePartChars, data.Span, out errorCode, out errorMessage, out _))
		{
			return false;
		}

		humanReadablePart = humanReadablePartChars.ToString();

		if (length.Value.Data is < MinF4JumbleInputLength or > MaxF4JumbleInputLength)
		{
			errorCode = DecodeError.UnexpectedLength;
			errorMessage = Strings.InvalidAddressLength;
			return false;
		}

		F4Jumble(data.Span, inverted: true);

		// Verify the 16-byte padding is as expected.
		Span<byte> padding = stackalloc byte[PaddingLength];
		InitializePadding(humanReadablePart, padding);
		if (!data.Span[^padding.Length..].SequenceEqual(padding))
		{
			errorCode = DecodeError.BadPadding;
			errorMessage = Strings.InvalidPadding;
			return false;
		}

		// Strip the padding.
		data = data[..^padding.Length];

		List<UnknownElement> elementsList = new();
		elements = elementsList;
		while (data.Length > 0)
		{
			byte typeCode = data.Span[0];
			data = data[1..];
			data = data[CompactSize.Decode(data.Span, out ulong keyLengthUL)..];
			int elementLength = checked((int)keyLengthUL);

			// Process each receiver type we support, and quietly ignore any we don't.
			if (data.Length < elementLength)
			{
				errorCode = DecodeError.UnexpectedLength;
				errorMessage = $"Expected data length {elementLength} but remaining data had only {data.Length} bytes left.";
				return false;
			}

			elementsList.Add(new UnknownElement(typeCode, data[..elementLength]));

			// Move on to the next receiver.
			data = data[elementLength..];
		}

		errorCode = null;
		errorMessage = null;
		return true;
	}

	/// <summary>
	/// Applies the F4Jumble function to the specified buffer.
	/// </summary>
	/// <param name="ua">The buffer to mutate.</param>
	/// <param name="inverted"><see langword="true" /> to reverse the process.</param>
	/// <exception cref="ArgumentException">Thrown when the input buffer is shorter than <see cref="MinF4JumbleInputLength"/> or longer than <see cref="MaxF4JumbleInputLength"/>.</exception>
	/// <devremarks>
	/// <see href="https://docs.rs/f4jumble/latest/src/f4jumble/lib.rs.html#208">Some source for inspiration</see> while interpreting the spec.
	/// </devremarks>
	internal static void F4Jumble(Span<byte> ua, bool inverted = false)
	{
		if (ua.Length is < MinF4JumbleInputLength or > MaxF4JumbleInputLength)
		{
			throw new ArgumentException($"The UA F4Jumble length must fall within [{MinF4JumbleInputLength}, {MaxF4JumbleInputLength}] but was {ua.Length}.", nameof(ua));
		}

		Span<byte> arrayBuffer = stackalloc byte[ua.Length];
		ua.CopyTo(arrayBuffer);

		byte leftLength = (byte)Math.Min(F4OutputLength, ua.Length / 2);
		int rightLength = ua.Length - leftLength;
		Span<byte> hash = stackalloc byte[F4OutputLength];

		if (inverted)
		{
			RoundH(1, hash, arrayBuffer);
			RoundG(1, hash, arrayBuffer);
			RoundH(0, hash, arrayBuffer);
			RoundG(0, hash, arrayBuffer);
		}
		else
		{
			RoundG(0, hash, arrayBuffer);
			RoundH(0, hash, arrayBuffer);
			RoundG(1, hash, arrayBuffer);
			RoundH(1, hash, arrayBuffer);
		}

		arrayBuffer[..ua.Length].CopyTo(ua);

		void RoundG(byte i, Span<byte> hash, Span<byte> arrayBuffer)
		{
			Span<byte> personalization = stackalloc byte[StartingGPersonalization.Length];
			StartingGPersonalization.CopyTo(personalization);
			personalization[^3] = i;
			ushort top = checked((ushort)CeilDiv(rightLength, F4OutputLength));
			for (ushort j = 0; j < top; j++)
			{
				personalization[^2] = (byte)(j & 0xff);
				personalization[^1] = (byte)(j >> 8);
				Blake2B.Config config = new()
				{
					OutputSizeInBytes = F4OutputLength,
					Personalization = personalization,
				};
				int length = Blake2B.ComputeHash(arrayBuffer[..leftLength], hash, config);
				Xor(arrayBuffer[(leftLength + (j * F4OutputLength))..], hash[..length]);
			}
		}

		void RoundH(byte i, Span<byte> hash, Span<byte> arrayBuffer)
		{
			Span<byte> personalization = stackalloc byte[StartingHPersonalization.Length];
			StartingHPersonalization.CopyTo(personalization);
			personalization[^3] = i;
			Blake2B.Config config = new()
			{
				OutputSizeInBytes = leftLength,
				Personalization = personalization,
			};
			int length = Blake2B.ComputeHash(arrayBuffer.Slice(leftLength, rightLength), hash, config);
			Xor(arrayBuffer[..leftLength], hash[..length]);
		}

		static int CeilDiv(int number, int divisor) => (number + divisor - 1) / divisor;

		static void Xor(Span<byte> left, ReadOnlySpan<byte> right)
		{
			for (int i = 0; i < left.Length & i < right.Length; i++)
			{
				left[i] ^= right[i];
			}
		}
	}

	/// <summary>
	/// Initializes the padding buffer.
	/// </summary>
	/// <param name="humanReadablePart">The human readable part for the encoding.</param>
	/// <param name="padding">The buffer to write to. It must be at least 16 bytes.</param>
	/// <returns>The number of bytes written to <paramref name="padding"/>. Always 16.</returns>
	internal static int InitializePadding(ReadOnlySpan<char> humanReadablePart, Span<byte> padding)
	{
		int written = Encoding.UTF8.GetBytes(humanReadablePart, padding[0..PaddingLength]);
		padding[written..PaddingLength].Clear();
		return PaddingLength;
	}

	/// <summary>
	/// Represents an element read from a unified encoding.
	/// </summary>
	internal class UnknownElement : IUnifiedEncodingElement
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="UnknownElement"/> class.
		/// </summary>
		/// <param name="typeCode">The type code.</param>
		/// <param name="content">The content of the element.</param>
		internal UnknownElement(byte typeCode, ReadOnlyMemory<byte> content)
		{
			this.UnifiedTypeCode = typeCode;
			this.Content = content;
		}

		/// <inheritdoc/>
		public byte UnifiedTypeCode { get; }

		/// <inheritdoc/>
		public int UnifiedDataLength => this.Content.Length;

		/// <summary>
		/// Gets the content of the unknown element.
		/// </summary>
		public ReadOnlyMemory<byte> Content { get; }

		/// <inheritdoc/>
		public int WriteUnifiedData(Span<byte> destination) => this.Content.Span.CopyToRetLength(destination);
	}
}
