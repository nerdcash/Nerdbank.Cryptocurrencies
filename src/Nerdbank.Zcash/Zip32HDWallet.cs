// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Nerdbank.Cryptocurrencies;
using static Org.BouncyCastle.Crypto.Engines.SM2Engine;

namespace Nerdbank.Zcash;

/// <summary>
/// Shielded Hierarchical Deterministic Wallets as defined in
/// <see href="https://zips.z.cash/zip-0032">ZIP-32</see>.
/// </summary>
public partial class Zip32HDWallet
{
	/// <summary>
	/// Encodes a <see cref="BigInteger"/> as a byte sequence in little-endian order.
	/// </summary>
	/// <param name="value">The integer.</param>
	/// <param name="output">
	/// A buffer to fill with the encoded integer.
	/// Any excess bytes will be 0-padded.
	/// </param>
	/// <returns>The number of bytes written to <paramref name="output"/>. Always its length.</returns>
	/// <remarks>This is the inverse operation to <see cref="LEOS2IP(ReadOnlySpan{byte})"/>.</remarks>
	/// <exception cref="IndexOutOfRangeException">Thrown if <paramref name="output"/> is not large enough to store <paramref name="value"/>.</exception>
	private static int I2LEOSP(BigInteger value, Span<byte> output)
	{
		BigInteger byteSize = 256;
		int i = 0;
		for (; value > 0; i++)
		{
			(value, BigInteger remainder) = BigInteger.DivRem(value, byteSize);
			output[i] = (byte)remainder;
		}

		output[i..].Clear();
		return output.Length;
	}

	/// <summary>
	/// Decodes a <see cref="BigInteger"/> that has been encoded in little-endian order.
	/// </summary>
	/// <param name="input">A little-endian ordered encoding of an integer.</param>
	/// <remarks>This is the inverse operation to <see cref="I2LEOSP(BigInteger, Span{byte})"/></remarks>.
	private static BigInteger LEOS2IP(ReadOnlySpan<byte> input)
	{
		BigInteger result = default;
		BigInteger multiplier = 1;
		for (int i = 0; i < input.Length; i++, multiplier *= 256)
		{
			result += BigInteger.Multiply(input[i], multiplier);
		}

		return result;
	}

	/// <summary>
	/// Reverse the order of bits in each individual byte in a buffer.
	/// </summary>
	/// <param name="input">The byte sequence to convert. Each byte's individual bits are assumed to be in MSB to LSB order.</param>
	/// <param name="output">Receives the converted byte sequence, where each byte's bits are reversed so they are in LSB to MSB order.</param>
	/// <returns>The number of bytes written to <paramref name="output"/> (i.e. the length of <paramref name="input"/>.)</returns>
	/// <remarks>
	/// Convert each group of 8 bits in 
	/// to a byte value with the least significant bit first, and concatenate the resulting bytes in the same order as the groups.
	/// </remarks>
	private static int LEBS2OSP(ReadOnlySpan<byte> input, Span<byte> output)
	{
		for (int i = 0; i < input.Length; i++)
		{
			byte originalValue = input[i];
			output[i] = (byte)(
				(originalValue & 0x80 >> 7) |
				(originalValue & 0x40 >> 5) |
				(originalValue & 0x20 >> 3) |
				(originalValue & 0x10 >> 1) |
				(originalValue & 0x08 << 1) |
				(originalValue & 0x04 << 3) |
				(originalValue & 0x02 << 5) |
				(originalValue & 0x01 << 7));
		}

		return input.Length;
	}

	/// <summary>
	/// Applies a Blake2b_512 hash to the concatenation of a pair of buffers.
	/// </summary>
	/// <param name="sk">The first input buffer.</param>
	/// <param name="t">The second input buffer.</param>
	/// <param name="output">The buffer to receive the hash. Must be at least 64 bytes in length.</param>
	/// <returns>The number of bytes written to <paramref name="output"/>. Always 64.</returns>
	private static int PRFexpand(ReadOnlySpan<byte> sk, ReadOnlySpan<byte> t, Span<byte> output)
	{
		// Rather than copy the input data into a single buffer, we could use an instance of Blake2B and call Update on it once for each input buffer.
		Span<byte> buffer = stackalloc byte[sk.Length + t.Length];
		sk.CopyTo(buffer);
		t.CopyTo(buffer[sk.Length..]);
		return Blake2B.ComputeHash(buffer, output, new Blake2B.Config { Personalization = "Zcash_ExpandSeed"u8, OutputSizeInBytes = 512 / 8 });
	}


	/// <summary>
	/// An implementation of FF1-AES encryption.
	/// </summary>
	/// <remarks>
	/// This is as specified at <see href="https://nvlpubs.nist.gov/nistpubs/SpecialPublications/NIST.SP.800-38G.pdf">Recommendation for Block Cipher Modes of Operation</see>
	/// with parameters as specified in <see href="https://zips.z.cash/zip-0032#conventions">ZIP-32</see>.
	/// </remarks>
	private static void FF1AES256(ReadOnlySpan<byte> key, Span<byte> data)
	{
		const int Radix = 2;
		const int MinLen = 88;
		const int MaxLen = 88;
		const string Tweak = "";

		if (key.Length != 256 / 8)
		{
			throw new ArgumentException(Strings.FormatUnexpectedLength(256 / 8, key.Length));
		}

		if (data.Length != 88 / 8)
		{
			throw new ArgumentException(Strings.FormatUnexpectedLength(88 / 8, data.Length));
		}

		int n = data.Length * 8;
		int u = n / 2;
		int v = n - u;

		// questions:
		// The spec calls for a 128-bit cipher. Does using AES256 require any other deviations from the spec?
		// What does the ⊕ symbol mean?
		// What does [1]^16 mean?
		// How should we split 44 bits (5.5 bytes)?

		Aes aes = Aes.Create();
		throw new NotImplementedException();
	}
}
