// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using NBitcoin.Secp256k1;

namespace Nerdbank.Cryptocurrencies.Bitcoin;

/// <summary>
/// An EC private key.
/// </summary>
internal class PrivateKey : IDisposable, IKey
{
	/// <summary>
	/// Initializes a new instance of the <see cref="PrivateKey"/> class.
	/// </summary>
	/// <param name="key">The underlying cryptographic key.</param>
	/// <param name="isTestNet">A value indicating whether this key operates on the testnet.</param>
	internal PrivateKey(ECPrivKey key, bool isTestNet)
	{
		this.CryptographicKey = key;
		this.PublicKey = new(this.CryptographicKey.CreatePubKey());
		this.IsTestNet = isTestNet;
	}

	/// <inheritdoc/>
	public bool IsTestNet { get; }

	/// <summary>
	/// Gets the underlying cryptographic key.
	/// </summary>
	internal ECPrivKey CryptographicKey { get; }

	/// <summary>
	/// Gets the public key counterpart to this private key.
	/// </summary>
	internal PublicKey PublicKey { get; }

	/// <inheritdoc/>
	public void Dispose() => this.CryptographicKey.Dispose();

	/// <inheritdoc cref="BitcoinUtilities.EncodePrivateKey(ReadOnlySpan{byte})"/>
	public override string ToString()
	{
		Span<byte> keyMaterial = stackalloc byte[32];
		this.CryptographicKey.sec.WriteToSpan(keyMaterial);
		return BitcoinUtilities.EncodePrivateKey(keyMaterial);
	}
}
