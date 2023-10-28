// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Bitcoin;

namespace Nerdbank.Zcash;

public partial class Zip32HDWallet
{
	/// <summary>
	/// Contains types and methods related to the Transparent pool.
	/// </summary>
	public static partial class Transparent
	{
		/// <inheritdoc cref="Create(ReadOnlySpan{byte}, ZcashNetwork)"/>
		/// <param name="mnemonic">The mnemonic phrase from which to generate the master key.</param>
		/// <param name="network"><inheritdoc cref="Create(ReadOnlySpan{byte}, ZcashNetwork)" path="/param[@name='network']"/></param>
		public static ExtendedSpendingKey Create(Bip39Mnemonic mnemonic, ZcashNetwork network) => Create(Requires.NotNull(mnemonic).Seed, network);

		/// <summary>
		/// Creates a master key for the Transparent pool.
		/// </summary>
		/// <param name="seed">The seed for use to generate the master key. A given seed will always produce the same master key.</param>
		/// <param name="network">The network this key should be used with.</param>
		/// <returns>A master extended spending key.</returns>
		public static ExtendedSpendingKey Create(ReadOnlySpan<byte> seed, ZcashNetwork network)
		{
			return new ExtendedSpendingKey(Bip32HDWallet.ExtendedPrivateKey.Create(seed, network.IsTestNet()), network);
		}
	}
}
