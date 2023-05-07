// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Cryptocurrencies;

public static partial class Bip32HDWallet
{
	internal class PrivateKey : IDisposable
	{
		internal PrivateKey(NBitcoin.Secp256k1.ECPrivKey key)
		{
			this.Key = key;
		}

		internal NBitcoin.Secp256k1.ECPrivKey Key { get; }

		/// <inheritdoc/>
		public void Dispose() => this.Key.Dispose();

		internal PublicKey CreatePublicKey() => new(this.Key.CreatePubKey());
	}
}
