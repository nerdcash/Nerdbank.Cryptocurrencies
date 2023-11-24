// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash;

/// <summary>
/// Internal utilities for this library.
/// </summary>
public static class ZcashUtilities
{
	/// <summary>
	/// The number of ZATs in a ZEC.
	/// </summary>
	internal const uint ZatsPerZEC = 100_000_000;

	/// <summary>
	/// Gets the ticker symbol to use iven a Zcash network (e.g. ZEC or TAZ).
	/// </summary>
	/// <param name="network">The Zcash network.</param>
	/// <returns>The ticker symbol (either ZEC or TAZ).</returns>
	/// <exception cref="ArgumentException">Thrown if <paramref name="network"/> isn't a recognized value.</exception>
	public static string GetTickerName(this ZcashNetwork network)
	{
		return network switch
		{
			ZcashNetwork.MainNet => "ZEC",
			ZcashNetwork.TestNet => "TAZ",
			_ => throw new ArgumentException(Strings.FormatUnrecognizedNetwork(network), nameof(network)),
		};
	}

	/// <summary>
	/// Gets the <see cref="Security" /> object for a given Zcash network.
	/// </summary>
	/// <param name="network">The zcash network.</param>
	/// <returns>A <see cref="Security"/> value.</returns>
	/// <exception cref="ArgumentException">Thrown if the <paramref name="network"/> value isn't recognized.</exception>
	public static Security AsSecurity(this ZcashNetwork network)
	{
		return network switch
		{
			ZcashNetwork.MainNet => Security.ZEC,
			ZcashNetwork.TestNet => Security.TAZ,
			_ => throw new ArgumentException(Strings.FormatUnrecognizedNetwork(network), nameof(network)),
		};
	}

	/// <summary>
	/// Parses an encoded key into its Zcash representation.
	/// </summary>
	/// <param name="encodedKey">A standard Zcash key encoding.</param>
	/// <param name="key">Receives the instantiated key, if parsing is successful and the key type is recognized as related to Zcash.</param>
	/// <returns><see langword="true" /> if <paramref name="encodedKey"/> was recognized as an encoding of some Zcash-related key; <see langword="false" /> otherwise.</returns>
	/// <remarks>
	/// <listheader>Recognized encodings include:</listheader>
	/// <list type="bullet">
	/// <item>Unified viewing keys (<c>uview</c> or <c>uivk</c>)</item>
	/// <item>Orchard extended spending keys (<c>secret-orchard-extsk-main</c>)</item>
	/// <item>Sapling extended spending keys (<c>secret-extended-key-main</c>)</item>
	/// <item>Sapling extended full viewing keys (<c>zxviews</c>)</item>
	/// <item>Sapling full viewing keys (<c>zviews</c>)</item>
	/// <item>Sapling incoming viewing keys (<c>zivks</c>)</item>
	/// <item>Transparent spending keys (<c>xprv</c>)</item>
	/// <item>Transparent viewing keys (<c>xpub</c>)</item>
	/// </list>
	/// <para>Both mainnet and testnet encodings are supported.</para>
	/// </remarks>
	public static bool TryParseKey(string encodedKey, [NotNullWhen(true)] out IKeyWithTextEncoding? key)
	{
		if (UnifiedViewingKey.TryDecode(encodedKey, out _, out _, out UnifiedViewingKey? unifiedViewingKey))
		{
			key = unifiedViewingKey;
		}
		else if (Zip32HDWallet.Orchard.ExtendedSpendingKey.TryDecode(encodedKey, out _, out _, out Zip32HDWallet.Orchard.ExtendedSpendingKey? orchardSpendingKey))
		{
			key = orchardSpendingKey;
		}
		else if (Zip32HDWallet.Sapling.ExtendedSpendingKey.TryDecode(encodedKey, out _, out _, out Zip32HDWallet.Sapling.ExtendedSpendingKey? saplingSpendingKey))
		{
			key = saplingSpendingKey;
		}
		else if (Zip32HDWallet.Sapling.ExtendedFullViewingKey.TryDecode(encodedKey, out _, out _, out Zip32HDWallet.Sapling.ExtendedFullViewingKey? saplingEFVK))
		{
			key = saplingEFVK;
		}
		else if (Sapling.FullViewingKey.TryDecode(encodedKey, out _, out _, out Sapling.FullViewingKey? saplingFVK))
		{
			key = saplingFVK;
		}
		else if (Sapling.IncomingViewingKey.TryDecode(encodedKey, out _, out _, out Sapling.IncomingViewingKey? saplingIVK))
		{
			key = saplingIVK;
		}
		else if (Zip32HDWallet.Transparent.ExtendedSpendingKey.TryDecode(encodedKey, out _, out _, out Zip32HDWallet.Transparent.ExtendedSpendingKey? transparentExtPrivateKey))
		{
			key = transparentExtPrivateKey;
		}
		else if (Zip32HDWallet.Transparent.ExtendedViewingKey.TryDecode(encodedKey, out _, out _, out Zip32HDWallet.Transparent.ExtendedViewingKey? transparentExtViewingKey))
		{
			key = transparentExtViewingKey;
		}
		else if (Transparent.PrivateKey.TryDecode(encodedKey, out _, out _, out Transparent.PrivateKey? transparentPrivateKey))
		{
			key = transparentPrivateKey;
		}
		else
		{
			key = null;
		}

		return key is not null;
	}

	/// <summary>
	/// Converts ZATs to ZEC.
	/// </summary>
	/// <param name="zats">The amount in ZATs.</param>
	/// <returns>The amount in ZEC.</returns>
	internal static decimal ZatsToZEC(ulong zats) => (decimal)zats / ZatsPerZEC;

	/// <summary>
	/// Translates an internal <see cref="DecodeError"/> to a public <see cref="ParseError"/>.
	/// </summary>
	/// <param name="decodeError">The decode error.</param>
	/// <returns>The parse error to report to the user.</returns>
	[return: NotNullIfNotNull(nameof(decodeError))]
	internal static ParseError? ToParseError(this DecodeError? decodeError)
	{
		return decodeError switch
		{
			null => null,
			DecodeError.BufferTooSmall => throw Assumes.Fail("An internal error occurred: the buffer was too small."),
			_ => ParseError.InvalidAddress,
		};
	}

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
	internal static int PRFexpand(ReadOnlySpan<byte> first, PrfExpandCodes domainSpecifier, Span<byte> output) => PRFexpand(first, domainSpecifier, default, output);

	/// <summary>
	/// Applies a Blake2b_512 hash to the concatenation of a pair of buffers.
	/// </summary>
	/// <param name="first">The first input buffer.</param>
	/// <param name="domainSpecifier">The byte that is unique for the caller's purpose.</param>
	/// <param name="second">The second input buffer.</param>
	/// <param name="output">The buffer to receive the hash. Must be at least 64 bytes in length.</param>
	/// <returns>The number of bytes written to <paramref name="output"/>. Always 64.</returns>
	internal static int PRFexpand(ReadOnlySpan<byte> first, PrfExpandCodes domainSpecifier, ReadOnlySpan<byte> second, Span<byte> output)
	{
		Requires.Argument(output.Length >= 64, nameof(output), SharedStrings.FormatUnexpectedLength(64, output.Length));

		// Rather than copy the input data into a single buffer, we could use an instance of Blake2B and call Update on it once for each input buffer.
		Span<byte> buffer = stackalloc byte[first.Length + 1 + second.Length];
		first.CopyTo(buffer);
		buffer[first.Length] = (byte)domainSpecifier;
		second.CopyTo(buffer[(first.Length + 1)..]);
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
