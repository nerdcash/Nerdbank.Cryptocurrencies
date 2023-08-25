// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash;

/// <summary>
/// Internal utilities for this library.
/// </summary>
internal static class ZcashUtilities
{
	/// <summary>
	/// Converts a .NET <see cref="System.Numerics.BigInteger"/> to the BouncyCastle equivalent.
	/// </summary>
	/// <param name="value">The big integer to convert.</param>
	/// <returns>The BouncyCastle representation of the big integer.</returns>
	internal static Org.BouncyCastle.Math.BigInteger ToBouncyCastle(this System.Numerics.BigInteger value)
	{
		Span<byte> bytes = stackalloc byte[(int)(value.GetBitLength() / 8) + 1];
		Assumes.True(value.TryWriteBytes(bytes, out int bytesWritten, isUnsigned: true, isBigEndian: true));
		return new(value.Sign, bytes);
	}

	/// <summary>
	/// Converts a BouncyCastle <see cref="Org.BouncyCastle.Math.BigInteger"/> to its .NET Numerics equivalent.
	/// </summary>
	/// <param name="value">The big integer to convert.</param>
	/// <returns>The .NET representation of the bg integer.</returns>
	internal static System.Numerics.BigInteger ToNumerics(this Org.BouncyCastle.Math.BigInteger value)
	{
		Span<byte> bytes = stackalloc byte[value.GetLengthofByteArrayUnsigned()];
		value.ToByteArray(bytes);
		return new(bytes, isUnsigned: false, isBigEndian: true);
	}

	/// <inheritdoc cref="PRFexpand(ReadOnlySpan{byte}, PrfExpandCodes, ReadOnlySpan{byte}, Span{byte})"/>
	internal static int PRFexpand(ReadOnlySpan<byte> sk, PrfExpandCodes domainSpecifier, Span<byte> output) => PRFexpand(sk, domainSpecifier, default, output);

	/// <summary>
	/// Applies a Blake2b_512 hash to the concatenation of a pair of buffers.
	/// </summary>
	/// <param name="sk">The first input buffer.</param>
	/// <param name="domainSpecifier">The byte that is unique for the caller's purpose.</param>
	/// <param name="t">The second input buffer.</param>
	/// <param name="output">The buffer to receive the hash. Must be at least 64 bytes in length.</param>
	/// <returns>The number of bytes written to <paramref name="output"/>. Always 64.</returns>
	internal static int PRFexpand(ReadOnlySpan<byte> sk, PrfExpandCodes domainSpecifier, ReadOnlySpan<byte> t, Span<byte> output)
	{
		Requires.Argument(output.Length >= 64, nameof(output), SharedStrings.FormatUnexpectedLength(64, output.Length));

		// Rather than copy the input data into a single buffer, we could use an instance of Blake2B and call Update on it once for each input buffer.
		Span<byte> buffer = stackalloc byte[sk.Length + 1 + t.Length];
		sk.CopyTo(buffer);
		buffer[sk.Length] = (byte)domainSpecifier;
		t.CopyTo(buffer[(sk.Length + 1)..]);
		return Blake2B.ComputeHash(buffer, output, new Blake2B.Config { Personalization = "Zcash_ExpandSeed"u8, OutputSizeInBytes = 512 / 8 });
	}
}
