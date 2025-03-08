﻿// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Security.Cryptography;

namespace Nerdbank.Cryptocurrencies;

/// <summary>
/// Contains Base58Check encoding and decoding methods.
/// </summary>
/// <remarks>
/// Base58Check is exactly like Base58, except that a 4-byte checksum is appended to the end of the payload
/// prior to encoding as characters.
/// </remarks>
public static class Base58Check
{
	private const int ChecksumLength = 4;

	private static ReadOnlySpan<char> Alphabet => "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";

	/// <summary>
	/// Gets the maximum number of characters that can be encoded from a given number of bytes.
	/// </summary>
	/// <param name="byteCount">The number of bytes to be encoded.</param>
	/// <returns>The length of the buffer that should be allocated to encode.</returns>
	public static int GetMaxEncodedLength(int byteCount) => ((byteCount + ChecksumLength) * 138 / 100) + 1;

	/// <summary>
	/// Gets the maximum number of bytes that can be decoded from a given number of characters.
	/// </summary>
	/// <param name="charCount">The number of encoded characters.</param>
	/// <returns>The length of the buffer that should be allocated to decode.</returns>
	public static int GetMaxDecodedLength(int charCount) => (charCount * 733 / 1000) + 1;

	/// <summary>
	/// Encodes some data into a string.
	/// </summary>
	/// <param name="payload">The data to encode. This should usually include the 1-byte version header.</param>
	/// <param name="chars">Receives the encoded characters.</param>
	/// <returns>The number of characters written to <paramref name="chars"/>.</returns>
	public static int Encode(ReadOnlySpan<byte> payload, Span<char> chars)
	{
		// Compute checksum(payload)
		Span<byte> checksum = stackalloc byte[ChecksumLength];
		ComputeChecksum(payload, checksum);

		// Assemble as [payload, checksum(payload)]
		Span<byte> payloadAndChecksum = stackalloc byte[payload.Length + checksum.Length];
		payload.CopyTo(payloadAndChecksum);
		checksum.CopyTo(payloadAndChecksum[payload.Length..]);

		return EncodeRaw(payloadAndChecksum, chars);
	}

	/// <summary>
	/// Decodes a string to its original bytes.
	/// </summary>
	/// <param name="encoded">The encoded representation.</param>
	/// <param name="bytes">Receives the decoded bytes.</param>
	/// <returns>The number of bytes written to <paramref name="bytes" />.</returns>
	/// <exception cref="ArgumentException">Thrown if <paramref name="bytes"/> is not sufficiently large to store the decoded payload.</exception>
	/// <exception cref="FormatException">Thrown if the checksum fails to match or invalid characters are found.</exception>
	public static int Decode(ReadOnlySpan<char> encoded, Span<byte> bytes)
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
	/// Decodes a string to its original bytes.
	/// </summary>
	/// <param name="encoded">The encoded representation.</param>
	/// <param name="bytes">Receives the decoded bytes.</param>
	/// <param name="decodeError">Receives the error code of a failed decode operation.</param>
	/// <param name="errorMessage">Receives the error message of a failed decode operation.</param>
	/// <param name="bytesWritten">Receives the number of bytes written to <paramref name="bytes" />.</param>
	/// <returns>A value indicating whether decoding was successful.</returns>
	public static bool TryDecode(ReadOnlySpan<char> encoded, Span<byte> bytes, [NotNullWhen(false)] out DecodeError? decodeError, [NotNullWhen(false)] out string? errorMessage, out int bytesWritten)
	{
		Span<byte> rawBytes = stackalloc byte[encoded.Length + ChecksumLength];
		if (!TryDecodeRaw(encoded, rawBytes, out int decodedCount, out DecodeError? rawDecodeResult, out errorMessage))
		{
			decodeError = rawDecodeResult;
			bytesWritten = 0;
			return false;
		}

		if (decodedCount < 4)
		{
			bytesWritten = 0;
			decodeError = DecodeError.InvalidChecksum;
			errorMessage = Strings.DecodeInputTooShort;
			return false;
		}

		Span<byte> payload = rawBytes[..(decodedCount - ChecksumLength)];
		Span<byte> checksum = rawBytes.Slice(decodedCount - ChecksumLength, ChecksumLength);

		Span<byte> computedChecksum = stackalloc byte[ChecksumLength];
		ComputeChecksum(payload, computedChecksum);
		if (!computedChecksum.SequenceEqual(checksum))
		{
			bytesWritten = 0;
			decodeError = DecodeError.InvalidChecksum;
			errorMessage = Strings.InvalidChecksum;
			return false;
		}

		if (!payload.TryCopyTo(bytes))
		{
			bytesWritten = 0;
			decodeError = DecodeError.BufferTooSmall;
			errorMessage = Strings.TargetBufferTooSmall;
			return false;
		}

		bytesWritten = payload.Length;
		decodeError = null;
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
		var alphabetLength = new BigInteger(Alphabet.Length);
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

		chars[..charsWritten].Reverse();

		return charsWritten;
	}

	/// <summary>
	/// Decodes a string into a buffer, without regard to a version or checksum.
	/// </summary>
	/// <param name="chars">The characters to decode.</param>
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
				errorMessage = Strings.FormatInvalidCharXFoundAtY(ch, i + 1);
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

		payload[..leadingZerosCount].Clear();

		// Be unforgiving on this because our internal caller allocates this buffer.
		Assumes.True(number.TryWriteBytes(payload[leadingZerosCount..], out bytesWritten, isUnsigned: true, isBigEndian: true));

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
		hash[..ChecksumLength].CopyTo(checksum);
	}
}
