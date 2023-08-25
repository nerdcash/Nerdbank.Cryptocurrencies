// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using NBitcoin.Secp256k1;

namespace Nerdbank.Zcash.Transparent;

/// <summary>
/// A public key for the transparent pool.
/// </summary>
public class P2PKHFullViewingKey : IFullViewingKey
{
	/// <summary>
	/// Initializes a new instance of the <see cref="P2PKHFullViewingKey"/> class.
	/// </summary>
	/// <param name="key">The underlying cryptographic key.</param>
	/// <param name="network">The network this key should be used with.</param>
	public P2PKHFullViewingKey(ECPubKey key, ZcashNetwork network)
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

	/// <summary>
	/// Gets a public key that is derived to the <c>m/44'/coin_type'/account'/0</c> level.
	/// </summary>
	/// <remarks>
	/// Per ZIP-316, we can and should return the non-change derived address from here.
	/// </remarks>
	IIncomingViewingKey IFullViewingKey.IncomingViewingKey => throw new NotImplementedException();
}
