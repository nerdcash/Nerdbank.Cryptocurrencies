﻿// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Security.Cryptography;

namespace Nerdbank.Zcash;

/// <summary>
/// Contains Base58Check encoding and decoding methods.
/// </summary>
internal static class Base58Check
{
    private const int ChecksumLength = 4;

    /// <summary>
    /// The failure modes that may occur while decoding.
    /// </summary>
    internal enum DecodeError
    {
        /// <summary>
        /// A disallowed character was found in the encoded string.
        /// </summary>
        InvalidCharacter,

        /// <summary>
        /// The checksum failed to match.
        /// </summary>
        InvalidChecksum,

        /// <summary>
        /// The buffer to decode into was too small.
        /// </summary>
        BufferTooSmall,
    }

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
        if (!TryDecode(encoded, bytes, out DecodeError? error, out string? errorMessage, out int bytesWritten))
        {
            throw error switch
            {
                DecodeError.BufferTooSmall => new ArgumentException(errorMessage),
                _ => new FormatException(errorMessage),
            };
        }

        return bytesWritten;
    }

    /// <summary>
    /// Decodes a base58check encoded string to its original bytes.
    /// </summary>
    /// <param name="encoded">The encoded base58 representation.</param>
    /// <param name="bytes">Receives the decoded bytes.</param>
    /// <param name="decodeResult">Receives the error code of a failed decode operation.</param>
    /// <param name="errorMessage">Receives the error message of a failed decode operation.</param>
    /// <param name="bytesWritten">Receives the number of bytes written to <paramref name="bytes" />.</param>
    /// <returns>A value indicating whether decoding was successful.</returns>
    internal static bool TryDecode(ReadOnlySpan<char> encoded, Span<byte> bytes, [NotNullWhen(false)] out DecodeError? decodeResult, [NotNullWhen(false)] out string? errorMessage, out int bytesWritten)
    {
        Span<byte> rawBytes = stackalloc byte[encoded.Length + ChecksumLength];
        if (!TryDecodeRaw(encoded, rawBytes, out int decodedCount, out DecodeError? rawDecodeResult, out errorMessage))
        {
            decodeResult = rawDecodeResult;
            bytesWritten = 0;
            return false;
        }

        if (decodedCount < 4)
        {
            bytesWritten = 0;
            decodeResult = DecodeError.InvalidChecksum;
            errorMessage = "Too short to contain a checksum.";
            return false;
        }

        Span<byte> payload = rawBytes.Slice(0, decodedCount - ChecksumLength);
        Span<byte> checksum = rawBytes.Slice(decodedCount - ChecksumLength, ChecksumLength);

        Span<byte> computedChecksum = stackalloc byte[ChecksumLength];
        ComputeChecksum(payload, computedChecksum);
        if (!computedChecksum.SequenceEqual(checksum))
        {
            bytesWritten = 0;
            decodeResult = DecodeError.InvalidChecksum;
            errorMessage = "Checksum does not match.";
            return false;
        }

        payload.CopyTo(bytes);
        bytesWritten = payload.Length;
        decodeResult = null;
        errorMessage = null;
        return true;
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
    /// <param name="bytesWritten">The number of bytes written to <paramref name="payload"/>.</param>
    /// <param name="errorCode">Receives the error code when a failure occurs.</param>
    /// <param name="errorMessage">Receives the error message if a failure occurs.</param>
    /// <returns>A value indicating whether the decoding was successful.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="payload"/> is not sufficiently large to store the decoded payload.</exception>
    private static bool TryDecodeRaw(ReadOnlySpan<char> chars, Span<byte> payload, out int bytesWritten, [NotNullWhen(false)] out DecodeError? errorCode, [NotNullWhen(false)] out string? errorMessage)
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
                errorMessage = $"Non-base58 character: {ch} found at position {i + 1}.";
                bytesWritten = 0;
                errorCode = DecodeError.InvalidCharacter;
                return false;
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

        if (!number.TryWriteBytes(payload.Slice(leadingZerosCount), out bytesWritten, isUnsigned: true, isBigEndian: true))
        {
            errorMessage = "Target buffer is not large enough to hold the decoded payload.";
            bytesWritten = 0;
            errorCode = DecodeError.BufferTooSmall;
            return false;
        }

        errorMessage = null;
        bytesWritten += leadingZerosCount;
        errorCode = null;
        return true;
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
