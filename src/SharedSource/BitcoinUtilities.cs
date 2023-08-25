// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Cryptocurrencies;

/// <summary>
/// Utility methods for Bitcoin.
/// </summary>
internal static class BitcoinUtilities
{
	/// <summary>
	/// Gets the standard Bitcoin encoding of this private key.
	/// </summary>
	/// <param name="keyMaterial">The 32-byte key material for the bitcoin private key.</param>
	/// <returns>The base58check encoding of the private key.</returns>
	/// <remarks>
	/// This encoding is <see href="https://en.bitcoin.it/wiki/Base58Check_encoding#Encoding_a_private_key">as specified</see>.
	/// </remarks>
	internal static string EncodePrivateKey(ReadOnlySpan<byte> keyMaterial)
	{
		Span<byte> versionAndPayload = stackalloc byte[1 + keyMaterial.Length];
		versionAndPayload[0] = 0x80;
		keyMaterial.CopyTo(versionAndPayload[1..]);
		Span<char> encoding = stackalloc char[Base58Check.GetMaxEncodedLength(versionAndPayload.Length)];
		int length = Base58Check.Encode(versionAndPayload, encoding);
		return encoding[..length].ToString();
	}
}
