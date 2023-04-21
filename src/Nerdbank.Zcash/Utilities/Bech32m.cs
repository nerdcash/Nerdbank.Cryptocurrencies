// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace Nerdbank.Zcash.Utilities;

/// <summary>
/// Contains <see href="https://github.com/bitcoin/bips/blob/master/bip-0350.mediawiki">Bech32m (BIP-350)</see> encoding and decoding methods.
/// </summary>
internal static class Bech32m
{
    private const int ChecksumLength = 0;

    /// <summary>
    /// Gets the maximum number of characters that can be encoded from a given number of bytes.
    /// </summary>
    /// <param name="byteCount">The number of bytes to be encoded.</param>
    /// <returns>The length of the buffer that should be allocated to encode.</returns>
    internal static int GetMaximumEncodedLength(int byteCount) => throw new NotImplementedException();

    /// <summary>
    /// Gets the maximum number of bytes that can be decoded from a given number of characters.
    /// </summary>
    /// <param name="charCount">The number of encoded characters.</param>
    /// <returns>The length of the buffer that should be allocated to decode.</returns>
    internal static int GetMaximumDecodedLength(int charCount) => throw new NotImplementedException();

    /// <summary>
    /// Encodes some data into a string.
    /// </summary>
    /// <param name="payload">The data to encode. This should usually include the 1-byte version header.</param>
    /// <param name="chars">Receives the encoded characters.</param>
    /// <returns>The number of characters written to <paramref name="chars"/>.</returns>
    internal static int Encode(ReadOnlySpan<byte> payload, Span<char> chars)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Decodes a string to its original bytes.
    /// </summary>
    /// <param name="encoded">The encoded representation.</param>
    /// <param name="bytes">Receives the decoded bytes.</param>
    /// <returns>The number of bytes written to <paramref name="bytes" />.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="bytes"/> is not sufficiently large to store the decoded payload.</exception>
    /// <exception cref="FormatException">Thrown if the checksum fails to match or invalid characters are found.</exception>
    internal static int Decode(ReadOnlySpan<char> encoded, Span<byte> bytes)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Decodes a string to its original bytes.
    /// </summary>
    /// <param name="encoded">The encoded representation.</param>
    /// <param name="bytes">Receives the decoded bytes.</param>
    /// <param name="decodeResult">Receives the error code of a failed decode operation.</param>
    /// <param name="errorMessage">Receives the error message of a failed decode operation.</param>
    /// <param name="bytesWritten">Receives the number of bytes written to <paramref name="bytes" />.</param>
    /// <returns>A value indicating whether decoding was successful.</returns>
    internal static bool TryDecode(ReadOnlySpan<char> encoded, Span<byte> bytes, [NotNullWhen(false)] out DecodeError? decodeResult, [NotNullWhen(false)] out string? errorMessage, out int bytesWritten)
    {
        throw new NotImplementedException();
    }
}
