// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Nerdbank.Cryptocurrencies;

/// <summary>
/// Contains <see href="https://zips.z.cash/zip-0173">Bech32 (ZIP-173)</see>
/// and <see href="https://github.com/bitcoin/bips/blob/master/bip-0350.mediawiki">Bech32m (BIP-350)</see> encoding and decoding methods.
/// </summary>
/// <remarks>
/// Bech32(m) is a base32 encoding with a 6-byte checksum.
/// </remarks>
public sealed class Bech32
{
	/// <summary>
	/// Gets the instance that implements <see href="https://zips.z.cash/zip-0173">Bech32 (ZIP-173)</see>.
	/// </summary>
	public static readonly Bech32 Original = new(Bech32Const);

	/// <summary>
	/// Gets the instance that implements <see href="https://github.com/bitcoin/bips/blob/master/bip-0350.mediawiki">Bech32m (BIP-350)</see>.
	/// </summary>
	public static readonly Bech32 Bech32m = new(Bech32mConst);

	private const uint Bech32Const = 1;
	private const uint Bech32mConst = 0x2bc830a3;
	private const char TagDataSeparator = '1';
	private const int ChecksumLength = 6;

	private static readonly ReadOnlyMemory<uint> Gen = new uint[] { 0x3b6a57b2, 0x26508e6d, 0x1ea119fa, 0x3d4233dd, 0x2a1462b3 };

	private readonly uint powerConstant;

	/// <summary>
	/// Initializes a new instance of the <see cref="Bech32"/> class.
	/// </summary>
	/// <param name="powerConstant">The constant to use when computing or verifying the hash. Should be either <see cref="Bech32Const"/> or <see cref="Bech32mConst"/>.</param>
	private Bech32(uint powerConstant)
	{
		this.powerConstant = powerConstant;
	}

	[Flags]
	private enum Casing
	{
		None = 0,
		Upper = 0x1,
		Lower = 0x2,
		Both = Upper | Lower,
	}

	private static ReadOnlySpan<char> Alphabet => "qpzry9x8gf2tvdw0s3jn54khce6mua7l";

	/// <summary>
	/// Gets the maximum number of characters that can be encoded from a given number of bytes.
	/// </summary>
	/// <param name="tagLength">The number of characters in the tag to be encoded.</param>
	/// <param name="dataLength">The number of bytes in the data portion to be encoded.</param>
	/// <returns>The length of the buffer that should be allocated to encode.</returns>
	public static int GetEncodedLength(int tagLength, int dataLength) => tagLength + 1 + (int)Math.Ceiling((double)dataLength * 8 / 5) + 6;

	/// <summary>
	/// Gets the maximum number of bytes that can be decoded from a given sequence of characters.
	/// </summary>
	/// <param name="encoded">The Bech32 characters to decode.</param>
	/// <returns>
	/// The length of the buffers that should be allocated to decode.
	/// Will be <see langword="null" /> if the encoding is invalid to the point that the lengths cannot be determined.
	/// A non-<see langword="null" /> result does not guarantee that the encoding is valid.
	/// </returns>
	public static (int Tag, int Data)? GetDecodedLength(ReadOnlySpan<char> encoded)
	{
		int separatorIdx = encoded.LastIndexOf(TagDataSeparator);
		if (separatorIdx == -1)
		{
			return null;
		}

		int dataLength = (encoded.Length - separatorIdx - 1 - ChecksumLength) * 5 / 8;
		if (dataLength < 0)
		{
			return null;
		}

		return (separatorIdx, dataLength);
	}

	/// <summary>
	/// Encodes some data into a string.
	/// </summary>
	/// <param name="tag">The human readable part. Must not be empty.</param>
	/// <param name="data">The data to encode.</param>
	/// <param name="output">Receives the encoded characters.</param>
	/// <returns>The number of characters written to <paramref name="output"/>.</returns>
	public int Encode(ReadOnlySpan<char> tag, ReadOnlySpan<byte> data, Span<char> output)
	{
		Requires.Argument(!tag.IsEmpty, nameof(tag), Strings.HumanReadablePartRequired);

		// The encoding always starts with the tag and the separator.
		// The tag itself may contain a '1', so when decoding, the separator is the *last* 1 in the string.
		int outputBytesWritten = 0;
		outputBytesWritten += tag.CopyToRetLength(output);
		output[outputBytesWritten++] = TagDataSeparator;

		// Stretch the 8 bit wide data to 5 bit wide bytes.
		Span<byte> encodedWithChecksum = stackalloc byte[(data.Length * 8 / 5) + 1 + ChecksumLength];
		int written = Stretch8bitTo5bitBytes(data, encodedWithChecksum);

		// Append the checksum.
		written += this.CreateChecksum(tag, encodedWithChecksum[..written], encodedWithChecksum[written..]);

		// Convert the resulting encoding the characters and append them to the end.
		outputBytesWritten += MapToAlphabet(encodedWithChecksum[..written], output[outputBytesWritten..]);

		return outputBytesWritten;
	}

	/// <summary>
	/// Decodes a string to its original human readable part and data.
	/// </summary>
	/// <param name="encoded">The encoded representation. This <em>must not</em> be a mixed case string.</param>
	/// <param name="tag">Receives the human readable part.</param>
	/// <param name="data">Receives the decoded data.</param>
	/// <returns>The number of bytes written to <paramref name="tag"/> and <paramref name="data" />.</returns>
	/// <exception cref="ArgumentException">Thrown if <paramref name="tag"/> or <paramref name="data"/> is not sufficiently large to store the decoded payload.</exception>
	/// <exception cref="FormatException">Thrown if the checksum fails to match or invalid characters are found.</exception>
	public (int TagLength, int DataLength) Decode(ReadOnlySpan<char> encoded, Span<char> tag, Span<byte> data)
	{
		if (!this.TryDecode(encoded, tag, data, out DecodeError? errorCode, out string? errorMessage, out (int Tag, int Data) length))
		{
			throw errorCode switch
			{
				DecodeError.BufferTooSmall => new ArgumentException(errorMessage),
				_ => new FormatException(errorMessage),
			};
		}

		return length;
	}

	/// <summary>
	/// Decodes a string to its original bytes.
	/// </summary>
	/// <param name="encoded">The encoded representation.</param>
	/// <param name="tag">Receives the decoded tag.</param>
	/// <param name="data">Receives the decoded data.</param>
	/// <param name="decodeResult">Receives the error code of a failed decode operation.</param>
	/// <param name="errorMessage">Receives the error message of a failed decode operation.</param>
	/// <param name="length">Receives the length written to <paramref name="tag" /> and <paramref name="data"/>.</param>
	/// <returns>A value indicating whether decoding was successful.</returns>
	public bool TryDecode(ReadOnlySpan<char> encoded, Span<char> tag, Span<byte> data, [NotNullWhen(false)] out DecodeError? decodeResult, [NotNullWhen(false)] out string? errorMessage, out (int Tag, int Data) length)
	{
		// Extract the tag first.
		int separatorIdx = encoded.LastIndexOf(TagDataSeparator);
		if (separatorIdx == -1)
		{
			decodeResult = DecodeError.NoSeparator;
			errorMessage = Strings.MissingBech32Separator;
			length = default;
			return false;
		}

		if (tag.Length < separatorIdx)
		{
			decodeResult = DecodeError.BufferTooSmall;
			errorMessage = Strings.Bech32TagBufferTooSmall;
			length = default;
			return false;
		}

		Casing casing = Casing.None;
		for (int i = 0; i < separatorIdx; i++)
		{
			if (char.IsUpper(encoded[i]))
			{
				casing |= Casing.Upper;
			}
			else if (char.IsLower(encoded[i]))
			{
				casing |= Casing.Lower;
			}

			tag[i] = char.ToLowerInvariant(encoded[i]);
		}

		if (casing == Casing.Both)
		{
			decodeResult = DecodeError.InvalidCharacter;
			errorMessage = Strings.MixedCaseBech32;
			length = default;
			return false;
		}

		// Do the simple character to 5 bit byte conversion of the data.
		Span<byte> dataAndChecksum5bitBytes = stackalloc byte[encoded.Length - separatorIdx - 1];
		if (!TryMapFromAlphabet(casing, encoded[(separatorIdx + 1)..], dataAndChecksum5bitBytes, out decodeResult, out errorMessage, out _))
		{
			length = default;
			return false;
		}

		// Verify the checksum.
		if (!this.VerifyChecksum(tag[..separatorIdx], dataAndChecksum5bitBytes))
		{
			decodeResult = DecodeError.InvalidChecksum;
			errorMessage = Strings.InvalidChecksum;
			length = default;
			return false;
		}

		// Compress the 5 bit bytes back to 8 bit bytes.
		ReadOnlySpan<byte> data5bitBytes = dataAndChecksum5bitBytes[..^6];
		bool result = TryCompress5bitTo8bitBytes(data5bitBytes, data, out int dataLength, out decodeResult, out errorMessage);
		length = (separatorIdx, dataLength);
		return result;
	}

	private static uint PolyMod(ReadOnlySpan<byte> buffer)
	{
		// Spec source:
		//// GEN = [0x3b6a57b2, 0x26508e6d, 0x1ea119fa, 0x3d4233dd, 0x2a1462b3]
		//// chk = 1
		//// for v in values:
		////   b = (chk >> 25)
		////   chk = (chk & 0x1ffffff) << 5 ^ v
		////   for i in range(5):
		////   chk ^= GEN[i] if ((b >> i) & 1) else 0
		//// return chk

		ReadOnlySpan<uint> genSpan = Gen.Span;
		uint chk = 1;
		foreach (byte v in buffer)
		{
			uint b = chk >> 25;
			chk = (chk & 0x1ffffff) << 5 ^ v;
			for (int i = 0; i < 5; i++)
			{
				chk ^= ((b >> i) & 1) != 0 ? genSpan[i] : 0;
			}
		}

		return chk;
	}

	/// <summary>
	/// Expands 8-bit characters into a byte buffer where each byte contain only 3 bits or 5 bits of real data.
	/// </summary>
	/// <param name="chars">
	/// The characters to expand.
	/// These <em>must</em> be narrow characters, whose value fit entirely in the lower 8-bits.
	/// These must also not include a null character.
	/// Failure to follow these rules will result in a result that cannot be interpreted.
	/// </param>
	/// <param name="bytes">A buffer that receives the expanded bytes. This should be at least <c>*2+1</c> the length of <paramref name="chars"/>.</param>
	/// <returns>The number of bytes actually written.</returns>
	/// <remarks>
	/// The human-readable part is processed by first feeding the higher 3 bits of each character's US-ASCII value
	/// into the checksum calculation followed by a zero and then the lower 5 bits of each US-ASCII value.
	/// </remarks>
	private static int Expand(ReadOnlySpan<char> chars, Span<byte> bytes)
	{
		// Spec source:
		//// return [ord(x) >> 5 for x in s] + [0] + [ord(x) & 31 for x in s]

		for (int i = 0; i < chars.Length; i++)
		{
			bytes[i] = (byte)(char.ToLowerInvariant(chars[i]) >> 5);
		}

		bytes[chars.Length] = 0;

		for (int i = 0; i < chars.Length; i++)
		{
			bytes[chars.Length + 1 + i] = (byte)(char.ToLowerInvariant(chars[i]) & 0x1f);
		}

		return (chars.Length * 2) + 1;
	}

	/// <summary>
	/// Spreads out the bits of 8-bit bytes into 5-bit bytes, where the 3 MSB of each byte are zero.
	/// </summary>
	/// <param name="input">The 8-bit byte input buffer.</param>
	/// <param name="output">The 5-bit byte output buffer. This must be at least <c>*8/5</c> the length of <paramref name="input"/>, rounding up.</param>
	/// <returns>The number of bytes written to <paramref name="output"/>.</returns>
	private static int Stretch8bitTo5bitBytes(ReadOnlySpan<byte> input, Span<byte> output)
	{
		// the number of complete bytes written to output.
		int bytesWritten = 0;

		// the number of bits written to the output at the bytesWritten+1 position.
		int bitsWritten = 0;

		for (int i = 0; i < input.Length; i++)
		{
			// The strategy here is to find places for 8 bits at a time,
			// which will always spread out across at least 2 bytes, sometimes 3.
			// We keep track of how many bits we've written beyond a byte boundary
			// so that on the next iteration we can pick up where we left off.
			switch (bitsWritten)
			{
				case 0:
					output[bytesWritten++] = (byte)(input[i] >> 3); // 5 bits
					output[bytesWritten] = (byte)((input[i] & 0x07) << 2); // 3 bits
					bitsWritten = 3;
					break;
				case 3:
					output[bytesWritten++] |= (byte)(input[i] >> 6); // 2 bits
					output[bytesWritten++] = (byte)((input[i] >> 1) & 0x1f); // 5 bits
					output[bytesWritten] = (byte)((input[i] & 0x01) << 4); // 1 bit
					bitsWritten = 1;
					break;
				case 1:
					output[bytesWritten++] |= (byte)(input[i] >> 4); // 4 bits
					output[bytesWritten] = (byte)((input[i] & 0x0f) << 1); // 4 bits
					bitsWritten = 4;
					break;
				case 4:
					output[bytesWritten++] |= (byte)(input[i] >> 7); // 1 bit
					output[bytesWritten++] = (byte)((input[i] >> 2) & 0x1f); // 5 bits
					output[bytesWritten] = (byte)((input[i] & 0x03) << 3); // 2 bits
					bitsWritten = 2;
					break;
				case 2:
					output[bytesWritten++] |= (byte)(input[i] >> 5); // 3 bits
					output[bytesWritten++] = (byte)(input[i] & 0x1f); // 5 bits
					bitsWritten = 0;
					break;
			}
		}

		if (bitsWritten != 0)
		{
			bytesWritten++;
		}

		return bytesWritten;
	}

	/// <summary>
	/// Condenses 5-bit bytes back into full 8-bit bytes.
	/// </summary>
	/// <param name="input">The 5-bit byte input buffer.</param>
	/// <param name="output">The 8-bit byte output buffer. This must be at least <c>*5/8</c> the length of <paramref name="input"/>.</param>
	/// <param name="outputLength">Receives the number of bytes written to <paramref name="output"/>.</param>
	/// <param name="errorCode">Receives the error code on failures.</param>
	/// <param name="errorMessage">Receives the error message on failures.</param>
	/// <returns>A value indicating whether decoding was successful.</returns>
	private static bool TryCompress5bitTo8bitBytes(ReadOnlySpan<byte> input, Span<byte> output, out int outputLength, [NotNullWhen(false)] out DecodeError? errorCode, [NotNullWhen(false)] out string? errorMessage)
	{
		if (input.Length == 0)
		{
			errorCode = null;
			errorMessage = null;
			outputLength = 0;
			return true;
		}

		if (output.Length < input.Length * 5 / 8)
		{
			errorCode = DecodeError.BufferTooSmall;
			errorMessage = "Output buffer is too small.";
			outputLength = 0;
			return false;
		}

		// The number of complete bytes written to output.
		int bytesWritten = 0;

		// The number of bits written to the output at the bytesWritten+1 position.
		int bitsWritten = 0;

		// Loop over the input up to and excluding the last byte, since the last byte may have padding that we don't want to write.
		for (int i = 0; i < input.Length - 1; i++)
		{
			switch (bitsWritten)
			{
				case 0:
					output[bytesWritten] = (byte)(input[i] << 3); // 5 bits
					bitsWritten = 5;
					break;
				case 5:
					output[bytesWritten++] |= (byte)(input[i] >> 2); // 3 bits
					output[bytesWritten] = (byte)(input[i] << 6); // 2 bits
					bitsWritten = 2;
					break;
				case 2:
					output[bytesWritten] |= (byte)(input[i] << 1); // 5 bits
					bitsWritten = 7;
					break;
				case 7:
					output[bytesWritten++] |= (byte)(input[i] >> 4); // 1 bit
					output[bytesWritten] = (byte)(input[i] << 4); // 4 bits
					bitsWritten = 4;
					break;
				case 4:
					output[bytesWritten++] |= (byte)(input[i] >> 1); // 4 bits
					output[bytesWritten] = (byte)(input[i] << 7); // 1 bit
					bitsWritten = 1;
					break;
				case 1:
					output[bytesWritten] |= (byte)(input[i] << 2); // 5 bits
					bitsWritten = 6;
					break;
				case 6:
					output[bytesWritten++] |= (byte)(input[i] >> 3); // 2 bits
					output[bytesWritten] = (byte)(input[i] << 5); // 3 bits
					bitsWritten = 3;
					break;
				case 3:
					output[bytesWritten++] |= input[i]; // 5 bits
					bitsWritten = 0;
					break;
			}
		}

		// Given we have up to 5 remaining bits, we expect to have non-empty slack in the last byte written not to exceed 5 bits.
		if (bitsWritten >= 3)
		{
			// Write the last byte, which may have padding.
			output[bytesWritten++] |= (byte)(input[^1] >> (bitsWritten - 3));

			// Ensure the unused bits in the last input byte are zero.
			if ((byte)(input[^1] << (8 - (bitsWritten - 3))) != 0)
			{
				errorCode = DecodeError.BadPadding;
				errorMessage = "Invalid padding in Bech32 encoding.";
				outputLength = 0;
				return false;
			}
		}
		else
		{
			outputLength = 0;
			errorCode = DecodeError.BadPadding;
			errorMessage = "Invalid length for Bech32 encoding.";
			return false;
		}

		errorCode = null;
		errorMessage = null;
		outputLength = bytesWritten;
		return true;
	}

	/// <summary>
	/// Encodes 5-bit bytes as characters using the Bech32 <see cref="Alphabet"/>.
	/// </summary>
	/// <param name="data">The bytes to encode.</param>
	/// <param name="output">Receives the encoded characters.</param>
	/// <returns>The number of characters written to <paramref name="output"/>.</returns>
	private static int MapToAlphabet(ReadOnlySpan<byte> data, Span<char> output)
	{
		for (int i = 0; i < data.Length; i++)
		{
			byte b = data[i];
			Debug.Assert((b & 0xe0) == 0, "Data has non-zero bits in the MSB 3 bits area.");
			output[i] = Alphabet[b];
		}

		return data.Length;
	}

	/// <summary>
	/// Decodes Bech32 <see cref="Alphabet"/> characters to 5-bit bytes.
	/// </summary>
	/// <param name="casing">The capitalization that has already been observed from the tag/HRP region of this bech32 encoding.</param>
	/// <param name="data">The encoded characters to decode.</param>
	/// <param name="output">Receives the decoded bytes.</param>
	/// <param name="decodeError">Receives the error code on failures.</param>
	/// <param name="errorMessage">Receives the error message on failures.</param>
	/// <param name="bytesWritten">The number of bytes written to <paramref name="output"/>.</param>
	/// <returns>A value indicating whether decoding was successful.</returns>
	private static bool TryMapFromAlphabet(Casing casing, ReadOnlySpan<char> data, Span<byte> output, [NotNullWhen(false)] out DecodeError? decodeError, [NotNullWhen(false)] out string? errorMessage, out int bytesWritten)
	{
		for (int i = 0; i < data.Length; i++)
		{
			if (char.IsUpper(data[i]))
			{
				casing |= Casing.Upper;
			}
			else if (char.IsLower(data[i]))
			{
				casing |= Casing.Lower;
			}

			char c = data[i];

			// Perf opportunity: create a reverse mapping array that directly translates the ordinal value of the character to the 5-bit value to use.
			int index = Alphabet.IndexOf(char.ToLowerInvariant(c));
			if (index < 0)
			{
				errorMessage = $"Invalid character '{c}' at index {i}.";
				decodeError = DecodeError.InvalidCharacter;
				bytesWritten = 0;
				return false;
			}

			output[i] = (byte)index;
		}

		if (casing == Casing.Both)
		{
			errorMessage = Strings.MixedCaseBech32;
			decodeError = DecodeError.InvalidCharacter;
			bytesWritten = 0;
			return false;
		}

		decodeError = null;
		errorMessage = null;
		bytesWritten = data.Length;
		return true;
	}

	/// <summary>
	/// Creates a checksum.
	/// </summary>
	/// <param name="tag">The human readable part.</param>
	/// <param name="data">The data.</param>
	/// <param name="checksum">Receives the 6-byte checksum.</param>
	/// <returns>The number of bytes written to <paramref name="checksum"/> (always 6).</returns>
	private int CreateChecksum(ReadOnlySpan<char> tag, ReadOnlySpan<byte> data, Span<byte> checksum)
	{
		// Spec source:
		//// values = bech32_hrp_expand(hrp) + data
		//// polymod = bech32_polymod(values + [0,0,0,0,0,0]) ^ 1
		//// return [(polymod >> 5 * (5 - i)) & 31 for i in range(6)]

		int expandedLength = (tag.Length * 2) + 1;
		Span<byte> values = stackalloc byte[expandedLength + data.Length + ChecksumLength];
		int written = Expand(tag, values[..expandedLength]);
		Debug.Assert(written == expandedLength, "Expand wrote an unexpected number of bytes.");
		data.CopyTo(values[expandedLength..]);
		uint polymod = PolyMod(values) ^ this.powerConstant;
		for (int i = 0; i < ChecksumLength; i++)
		{
			checksum[i] = (byte)((polymod >> (5 * (5 - i))) & 0x1f);
		}

		return ChecksumLength;
	}

	/// <summary>
	/// Verifies a checksum.
	/// </summary>
	/// <param name="tag">The human-readable part.</param>
	/// <param name="data">Additional data that feeds into the checksum, and the checksum itself.</param>
	/// <returns>A value indicating whether the checksum is correct.</returns>
	private bool VerifyChecksum(ReadOnlySpan<char> tag, ReadOnlySpan<byte> data)
	{
		// Spec source:
		// return bech32_polymod(bech32_hrp_expand(hrp) + data) == 1
		int expandedLength = (tag.Length * 2) + 1;
		Span<byte> values = stackalloc byte[expandedLength + data.Length];
		int written = Expand(tag, values[..expandedLength]);
		Debug.Assert(written == expandedLength, $"{nameof(Expand)} didn't write the expected number of bytes.");
		data.CopyTo(values[written..]);
		uint checksum = PolyMod(values);
		return checksum == this.powerConstant;
	}
}
