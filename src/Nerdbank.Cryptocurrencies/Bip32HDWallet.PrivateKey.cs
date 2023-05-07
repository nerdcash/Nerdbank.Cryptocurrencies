// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Cryptocurrencies;

public static partial class Bip32HDWallet
{
	/// <summary>
	/// An EC private key.
	/// </summary>
	internal class PrivateKey : IDisposable
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="PrivateKey"/> class.
		/// </summary>
		/// <param name="key">The underlying cryptographic key.</param>
		internal PrivateKey(NBitcoin.Secp256k1.ECPrivKey key)
		{
			this.Key = key;
		}

		/// <summary>
		/// Gets the underlying cryptographic key.
		/// </summary>
		internal NBitcoin.Secp256k1.ECPrivKey Key { get; }

		/// <inheritdoc/>
		public void Dispose() => this.Key.Dispose();

		/// <summary>
		/// Creates a public key counterpart to this private key.
		/// </summary>
		/// <returns>The public key.</returns>
		internal PublicKey CreatePublicKey() => new(this.Key.CreatePubKey());
	}
}
