// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using NBitcoin.Secp256k1;

namespace Nerdbank.Zcash.Transparent;

/// <summary>
/// A public key for the transparent pool.
/// </summary>
public class P2PKHPublicKey : IViewingKey
{
	/// <summary>
	/// Initializes a new instance of the <see cref="P2PKHPublicKey"/> class.
	/// </summary>
	/// <param name="key">The underlying cryptographic key.</param>
	/// <param name="network">The network this key should be used with.</param>
	public P2PKHPublicKey(ECPubKey key, ZcashNetwork network)
	{
		this.CryptographicKey = key;
		this.Network = network;
	}

	/// <summary>
	/// Gets the cryptographic key.
	/// </summary>
	public ECPubKey CryptographicKey { get; }

	/// <inheritdoc/>
	public ZcashNetwork Network { get; }

	/// <inheritdoc/>
	bool IViewingKey.IsFullViewingKey => true;
}
