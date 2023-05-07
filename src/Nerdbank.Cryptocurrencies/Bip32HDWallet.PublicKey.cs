// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Cryptocurrencies;

public static partial class Bip32HDWallet
{
	internal class PublicKey
	{
		internal PublicKey(NBitcoin.Secp256k1.ECPubKey key)
		{
			this.Key = key;
		}

		internal NBitcoin.Secp256k1.ECPubKey Key { get; }
	}
}
