// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using NBitcoin.Secp256k1;
using Org.BouncyCastle.Crypto.Digests;
using SHA256 = System.Security.Cryptography.SHA256;

namespace Nerdbank.Bitcoin;

/// <summary>
/// An elliptic curve public key.
/// </summary>
[DebuggerDisplay($"{{{nameof(DebuggerDisplay)},nq}}")]
public class PublicKey : IKey
{
	/// <summary>
	/// Initializes a new instance of the <see cref="PublicKey" /> class.
	/// </summary>
	/// <param name="key">The underlying cryptographic key.</param>
	/// <param name="isTestNet">A value indicating whether this key operates on the testnet.</param>
	public PublicKey(ECPubKey key, bool isTestNet)
	{
		this.CryptographicKey = key;
		this.IsTestNet = isTestNet;

		this.P2PKHAddress = new(key, isTestNet);
	}

	/// <inheritdoc/>
	public bool IsTestNet { get; }

	/// <summary>
	/// Gets the Bitcoin address associated with this public key.
	/// </summary>
	public BitcoinP2PKHAddress P2PKHAddress { get; }

	/// <summary>
	/// Gets the underlying cryptographic key.
	/// </summary>
	public ECPubKey CryptographicKey { get; }

	private string DebuggerDisplay => this.P2PKHAddress.TextEncoding;

	/// <summary>
	/// Encodes the public key hash to a given buffer.
	/// </summary>
	/// <param name="pubKey">The public key.</param>
	/// <param name="destination">Receives the public key hash. This must be at least 20 bytes.</param>
	/// <returns>The number of bytes written to <paramref name="destination"/>. This will always be 20.</returns>
	public static int CreatePublicKeyHash(ReadOnlySpan<byte> pubKey, Span<byte> destination)
	{
		const int BytesWritten = 20;

		// https://www.freecodecamp.org/news/how-to-create-a-bitcoin-wallet-address-from-a-private-key-eca3ddd9c05f/
		Span<byte> sha256HashBytes = stackalloc byte[32];
		Assumes.True(SHA256.HashData(pubKey, sha256HashBytes) == sha256HashBytes.Length);

		RipeMD160Digest digest = new();
		digest.BlockUpdate(sha256HashBytes);
		Assumes.True(digest.DoFinal(destination) == BytesWritten);

		return BytesWritten;
	}
}
