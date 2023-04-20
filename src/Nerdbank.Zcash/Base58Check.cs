// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Numerics;
using System.Security.Cryptography;

namespace Nerdbank.Zcash;

/// <summary>
/// Contains Base58Check encoding and decoding methods.
/// </summary>
internal static class Base58Check
{
    private const int ChecksumLength = 4;

    private static ReadOnlySpan<char> Alphabet => "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";

    /// <summary>
    /// Encodes some data into a Base58Check string.
    /// </summary>
    /// <param name="payload">The data to encode. This should usually include the 1-byte version header.</param>
    /// <param name="chars">Receives the encoded characters.</param>
    /// <returns>The number of characters written to <paramref name="chars"/>.</returns>
    internal static int Encode(ReadOnlySpan<byte> payload, Span<char> chars)
    {
        // Compute checksum(payload)
        Span<byte> checksum = stackalloc byte[ChecksumLength];
        ComputeChecksum(payload, checksum);

        // Assemble as [payload, checksum(payload)]
        Span<byte> payloadAndChecksum = stackalloc byte[payload.Length + checksum.Length];
        payload.CopyTo(payloadAndChecksum);
        checksum.CopyTo(payloadAndChecksum.Slice(payload.Length));

        return EncodeRaw(payloadAndChecksum, chars);
    }

    /// <summary>
    /// Decodes a base58check encoded string to its original bytes.
    /// </summary>
    /// <param name="encoded">The encoded base58 representation.</param>
    /// <param name="bytes">Receives the decoded bytes.</param>
    /// <returns>The number of bytes written to <paramref name="bytes" />.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="bytes"/> is not sufficiently large to store the decoded payload.</exception>
    /// <exception cref="FormatException">Thrown if the checksum fails to match or invalid characters are found.</exception>
    internal static int Decode(ReadOnlySpan<char> encoded, Span<byte> bytes)
    {
        Span<byte> rawBytes = stackalloc byte[encoded.Length + ChecksumLength];
        int decodedCount = DecodeRaw(encoded, rawBytes);

        if (decodedCount < 4)
        {
            throw new FormatException("Input too short to include a checksum.");
        }

        Span<byte> payload = rawBytes.Slice(0, decodedCount - ChecksumLength);
        Span<byte> checksum = rawBytes.Slice(decodedCount - ChecksumLength, ChecksumLength);

        Span<byte> computedChecksum = stackalloc byte[ChecksumLength];
        ComputeChecksum(payload, computedChecksum);
        if (!computedChecksum.SequenceEqual(checksum))
        {
            throw new FormatException("Base58Check encoded string has invalid checksum.");
        }

        payload.CopyTo(bytes);
        return payload.Length;
    }

    /// <summary>
    /// Encodes a buffer, without regard to a version or checksum.
    /// </summary>
    /// <param name="payload">The payload to encode.</param>
    /// <param name="chars">Receives the encoded characters.</param>
    /// <returns>The number of characters written to <paramref name="chars"/>.</returns>
    private static int EncodeRaw(ReadOnlySpan<byte> payload, Span<char> chars)
    {
        BigInteger number = new(payload, isUnsigned: true, isBigEndian: true);
        BigInteger alphabetLength = new BigInteger(Alphabet.Length);
        int charsWritten = 0;
        while (number > 0)
        {
            (number, BigInteger remainder) = BigInteger.DivRem(number, alphabetLength);
            chars[charsWritten++] = Alphabet[(int)remainder];
        }

        // Prepend the leading zeros, since the division method above will not include them.
        for (int i = 0; i < payload.Length && payload[i] == 0; i++)
        {
            chars[charsWritten++] = Alphabet[0];
        }

        chars.Slice(0, charsWritten).Reverse();

        return charsWritten;
    }

    /// <summary>
    /// Decodes a base58 encoded string into a buffer, without regard to a version or checksum.
    /// </summary>
    /// <param name="chars">The base58 characters to decode.</param>
    /// <param name="payload">Receives the decoded bytes.</param>
    /// <returns>The number of bytes written to <paramref name="payload"/>.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="payload"/> is not sufficiently large to store the decoded payload.</exception>
    private static int DecodeRaw(ReadOnlySpan<char> chars, Span<byte> payload)
    {
        BigInteger number = BigInteger.Zero;
        int leadingZerosCount = 0;
        bool encounteredNonZero = false;
        for (int i = 0; i < chars.Length; i++)
        {
            char ch = chars[i];
            int position = Alphabet.IndexOf(ch);
            if (position == -1)
            {
                throw new FormatException($"Non-base58 character: {ch} found at position {i + 1}.");
            }

            if (position == 0 && !encounteredNonZero)
            {
                leadingZerosCount++;
            }
            else
            {
                number = (number * 58) + position;
                encounteredNonZero = true;
            }
        }

        payload.Slice(0, leadingZerosCount).Clear();

        if (!number.TryWriteBytes(payload.Slice(leadingZerosCount), out int bytesWritten, isUnsigned: true, isBigEndian: true))
        {
            throw new ArgumentException("Target buffer is not large enough to hold the decoded payload.", nameof(payload));
        }

        return leadingZerosCount + bytesWritten;
    }

    /// <summary>
    /// Computes the checksum for the given data.
    /// </summary>
    /// <param name="payload">The data.</param>
    /// <param name="checksum">Receives the 4-byte checksum.</param>
    private static void ComputeChecksum(ReadOnlySpan<byte> payload, Span<byte> checksum)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(payload, hash);
        SHA256.HashData(hash, hash);
        hash.Slice(0, ChecksumLength).CopyTo(checksum);
    }
}
