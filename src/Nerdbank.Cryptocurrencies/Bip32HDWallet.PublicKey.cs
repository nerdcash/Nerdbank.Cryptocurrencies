// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Cryptocurrencies;

public static partial class Bip32HDWallet
{
	/// <summary>
	/// An elliptic curve public key.
	/// </summary>
	public class PublicKey
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="PublicKey" /> class.
		/// </summary>
		/// <param name="key">The underlying cryptographic key.</param>
		internal PublicKey(NBitcoin.Secp256k1.ECPubKey key)
		{
			this.Key = key;
		}

		/// <summary>
		/// Gets the underlying cryptographic key.
		/// </summary>
		public NBitcoin.Secp256k1.ECPubKey Key { get; }
	}
}
