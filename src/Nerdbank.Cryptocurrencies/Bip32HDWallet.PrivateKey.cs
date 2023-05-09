// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Cryptocurrencies;

public static partial class Bip32HDWallet
{
	/// <summary>
	/// An EC private key.
	/// </summary>
	public class PrivateKey : IDisposable
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="PrivateKey"/> class.
		/// </summary>
		/// <param name="key">The underlying cryptographic key.</param>
		internal PrivateKey(NBitcoin.Secp256k1.ECPrivKey key)
		{
			this.Key = key;
			this.PublicKey = new(this.Key.CreatePubKey());
		}

		/// <summary>
		/// Gets the underlying cryptographic key.
		/// </summary>
		public NBitcoin.Secp256k1.ECPrivKey Key { get; }

		/// <summary>
		/// Gets the public key counterpart to this private key.
		/// </summary>
		public PublicKey PublicKey { get; }

		/// <inheritdoc/>
		public void Dispose() => this.Key.Dispose();
	}
}
