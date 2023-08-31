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

	/// <summary>
	/// Strips any key material in excess of incoming viewing keys.
	/// </summary>
	/// <param name="ivk">The key.</param>
	/// <returns>The key that is <em>only</em> an incoming viewing key.</returns>
	internal static IIncomingViewingKey ReduceToOnlyIVK(this IIncomingViewingKey ivk)
	{
		if (ivk is IFullViewingKey fvk)
		{
			ivk = fvk.IncomingViewingKey;

			// The property we called MUST return an object that is not still a full viewing key,
			// but we'll assert it here because our caller wants to make sure the we don't leak data.
			Assumes.True(ivk is not IFullViewingKey || ivk is IFullOrIncomingViewingKey { IsFullViewingKey: false });
		}

		return ivk;
	}

	/// <summary>
	/// Strips any key material in excess of full viewing keys.
	/// </summary>
	/// <param name="fvk">The key.</param>
	/// <returns>The key that is <em>only</em> a full viewing key.</returns>
	internal static IFullViewingKey ReduceToOnlyFVK(this IFullViewingKey fvk)
	{
		if (fvk is ISpendingKey sk)
		{
			fvk = sk.FullViewingKey;

			// The property we called MUST return an object that is not still a spending key,
			// but we'll assert it here because our caller wants to make sure the we don't leak data.
			Assumes.False(fvk is ISpendingKey);
		}

		return fvk;
	}

	/// <summary>
	/// Checks whether a given Zcash network is one of its test nets.
	/// </summary>
	/// <param name="network">The Zcash network.</param>
	/// <returns>A boolean value.</returns>
	internal static bool IsTestNet(this ZcashNetwork network) => network != ZcashNetwork.MainNet;

	/// <summary>
	/// Filter nulls out of a list.
	/// </summary>
	/// <typeparam name="T">The type of values in the list.</typeparam>
	/// <param name="values">The values.</param>
	/// <returns>An array of non-null values.</returns>
	internal static T[] RemoveNulls<T>(params T?[] values)
		where T : class
	{
		return values.Where(v => v is not null).ToArray()!;
	}
}
