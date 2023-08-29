// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using NBitcoin.Secp256k1;

namespace Nerdbank.Cryptocurrencies.Bitcoin;

/// <summary>
/// An elliptic curve public key.
/// </summary>
internal class PublicKey : IKey
{
	/// <summary>
	/// Initializes a new instance of the <see cref="PublicKey" /> class.
	/// </summary>
	/// <param name="key">The underlying cryptographic key.</param>
	/// <param name="isTestNet">A value indicating whether this key operates on the testnet.</param>
	internal PublicKey(ECPubKey key, bool isTestNet)
	{
		this.CryptographicKey = key;
		this.IsTestNet = isTestNet;
	}

	/// <inheritdoc/>
	public bool IsTestNet { get; }

	/// <summary>
	/// Gets the underlying cryptographic key.
	/// </summary>
	internal ECPubKey CryptographicKey { get; }
}
