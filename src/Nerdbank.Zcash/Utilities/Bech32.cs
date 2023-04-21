// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Nerdbank.Zcash.Utilities;

/// <summary>
/// Contains <see href="https://zips.z.cash/zip-0173">Bech32 (ZIP-173)</see> encoding and decoding methods.
/// </summary>
/// <remarks>
/// Bech32 is a base32 encoding with a 6-byte checksum.
/// </remarks>
internal static class Bech32
{
    private const int ChecksumLength = 6;

    private static readonly ReadOnlyMemory<uint> Gen = new uint[] { 0x3b6a57b2, 0x26508e6d, 0x1ea119fa, 0x3d4233dd, 0x2a1462b3 };

    private static ReadOnlySpan<char> Alphabet => "qpzry9x8gf2tvdw0s3jn54khce6mua7l";

    /// <summary>
    /// Gets the maximum number of characters that can be encoded from a given number of bytes.
    /// </summary>
    /// <param name="tagLength">The number of characters in the tag to be encoded.</param>
    /// <param name="dataLength">The number of bytes in the data portion to be encoded.</param>
    /// <returns>The length of the buffer that should be allocated to encode.</returns>
    internal static int GetMaximumEncodedLength(int tagLength, int dataLength) => tagLength + 1 + ((dataLength * 8 / 5) + 1) + 6;

    /// <summary>
    /// Gets the maximum number of bytes that can be decoded from a given number of characters.
    /// </summary>
    /// <param name="charCount">The number of encoded characters.</param>
    /// <returns>The length of the buffer that should be allocated to decode.</returns>
    internal static int GetMaximumDecodedLength(int charCount) => throw new NotImplementedException();

    /// <summary>
    /// Encodes some data into a string.
    /// </summary>
    /// <param name="tag">The human readable part.</param>
    /// <param name="data">The data to encode.</param>
    /// <param name="output">Receives the encoded characters.</param>
    /// <returns>The number of characters written to <paramref name="output"/>.</returns>
    internal static int Encode(ReadOnlySpan<char> tag, ReadOnlySpan<byte> data, Span<char> output)
    {
        // The encoding always starts with the tag and the separator.
        // The tag itself may contain a '1', so when decoding, the separator is the *last* 1 in the string.
        tag.CopyTo(output);
        output[tag.Length] = '1';
        int outputBytesWritten = tag.Length + 1;

        // Stretch the 8 bit wide data to 5 bit wide bytes.
        Span<byte> encodedWithChecksum = stackalloc byte[(data.Length * 8 / 5) + 1 + ChecksumLength];
        int written = Stretch8bitTo5bitBytes(data, encodedWithChecksum);

        // Append the checksum.
        written += CreateChecksum(tag, encodedWithChecksum.Slice(0, written), encodedWithChecksum.Slice(written));

        // Convert the resulting encoding the characters and append them to the end.
        foreach (byte b in encodedWithChecksum)
        {
            Debug.Assert((b & 0xe0) == 0, "Data has non-zero bits in the MSB 3 bits area.");
            output[outputBytesWritten++] = Alphabet[b];
        }

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
    internal static (int TagLength, int DataLength) Decode(ReadOnlySpan<char> encoded, Span<char> tag, Span<byte> data)
    {
        if (!TryDecode(encoded, tag, data, out DecodeError? errorCode, out string? errorMessage, out var length))
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
    internal static bool TryDecode(ReadOnlySpan<char> encoded, Span<char> tag, Span<byte> data, [NotNullWhen(false)] out DecodeError? decodeResult, [NotNullWhen(false)] out string? errorMessage, out (int Tag, int Data) length)
    {
        throw new NotImplementedException();
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
            bytes[i] = (byte)(chars[i] >> 5);
        }

        bytes[chars.Length] = 0;

        for (int i = 0; i < chars.Length; i++)
        {
            bytes[chars.Length + 1 + i] = (byte)(chars[i] & 0x1f);
        }

        return (chars.Length * 2) + 1;
    }

    /// <summary>
    /// Creates a checksum.
    /// </summary>
    /// <param name="humanReadablePart">The human readable part.</param>
    /// <param name="data">The data.</param>
    /// <param name="checksum">Receives the 6-byte checksum.</param>
    /// <returns>The number of bytes written to <paramref name="checksum"/> (always 6).</returns>
    private static int CreateChecksum(ReadOnlySpan<char> humanReadablePart, ReadOnlySpan<byte> data, Span<byte> checksum)
    {
        // Spec source:
        //// values = bech32_hrp_expand(hrp) + data
        //// polymod = bech32_polymod(values + [0,0,0,0,0,0]) ^ 1
        //// return [(polymod >> 5 * (5 - i)) & 31 for i in range(6)]

        int expandedLength = (humanReadablePart.Length * 2) + 1;
        Span<byte> values = stackalloc byte[expandedLength + data.Length + ChecksumLength];
        int written = Expand(humanReadablePart, values.Slice(0, expandedLength));
        Debug.Assert(written == expandedLength, "Expand wrote an unexpected number of bytes.");
        data.CopyTo(values.Slice(expandedLength));
        uint polymod = PolyMod(values) ^ 1;
        for (int i = 0; i < ChecksumLength; i++)
        {
            checksum[i] = (byte)((polymod >> (5 * (5 - i))) & 0x1f);
        }

        return ChecksumLength;
    }

    /// <summary>
    /// Verifies a checksum.
    /// </summary>
    /// <param name="humanReadablePart">The characters in the human-readable part.</param>
    /// <param name="data">Additional data that feeds into the checksum.</param>
    /// <returns>A value indicating whether the checksum is correct.</returns>
    private static bool VerifyChecksum(ReadOnlySpan<char> humanReadablePart, ReadOnlySpan<byte> data)
    {
        // Spec source:
        // return bech32_polymod(bech32_hrp_expand(hrp) + data) == 1
        int expandedLength = (humanReadablePart.Length * 2) + 1;
        Span<byte> values = stackalloc byte[expandedLength + data.Length];
        int written = Expand(humanReadablePart, values.Slice(0, expandedLength));
        Debug.Assert(written == expandedLength, $"{nameof(Expand)} didn't write the expected number of bytes.");
        data.CopyTo(values.Slice(written));
        uint checksum = PolyMod(values);
        return checksum == 1;
    }

    private static int Stretch8bitTo5bitBytes(ReadOnlySpan<byte> input, Span<byte> output)
    {
        throw new NotImplementedException();
    }

    private static int Compress5bitTo8bitBytes(ReadOnlySpan<byte> input, Span<byte> output)
    {
        throw new NotImplementedException();
    }
}
