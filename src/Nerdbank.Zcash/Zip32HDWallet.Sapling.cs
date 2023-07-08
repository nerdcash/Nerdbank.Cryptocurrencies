// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Numerics;
using Org.BouncyCastle.Math.EC;

namespace Nerdbank.Zcash;

public partial class Zip32HDWallet
{
	/// <summary>
	/// Contains types and methods related to the Sapling pool.
	/// </summary>
	public static partial class Sapling
	{
		/// <inheritdoc cref="Create(ReadOnlySpan{byte}, bool)"/>
		/// <param name="mnemonic">The mnemonic phrase from which to generate the master key.</param>
		/// <param name="testNet"><inheritdoc cref="Create(ReadOnlySpan{byte}, bool)" path="/param[@name='testNet']"/></param>
		public static ExtendedSpendingKey Create(Bip39Mnemonic mnemonic, bool testNet = false) => Create(Requires.NotNull(mnemonic).Seed, testNet);

		/// <summary>
		/// Creates a master key for the Sapling pool.
		/// </summary>
		/// <param name="seed">The seed byte sequence, which MUST be at least 32 and at most 252 bytes.</param>
		/// <param name="testNet"><see langword="true" /> when the generated key will be used to interact with the zcash testnet; <see langword="false" /> otherwise.</param>
		/// <returns>The master extended spending key.</returns>
		public static ExtendedSpendingKey Create(ReadOnlySpan<byte> seed, bool testNet = false)
		{
			Span<byte> blakeOutput = stackalloc byte[64]; // 512 bits
			Blake2B.ComputeHash(seed, blakeOutput, new Blake2B.Config { Personalization = "ZcashIP32Sapling"u8, OutputSizeInBytes = blakeOutput.Length });
			Span<byte> spendingKey = blakeOutput[..32];
			ChainCode chainCode = new(blakeOutput[32..]);

			Span<byte> expandedSpendingKey = stackalloc byte[96];
			NativeMethods.GetSaplingExpandedSpendingKey(spendingKey, expandedSpendingKey);
			ReadOnlySpan<byte> ask = expandedSpendingKey[..32];
			ReadOnlySpan<byte> nsk = expandedSpendingKey[32..64];

			Span<byte> expandOutput = stackalloc byte[64];
			PRFexpand(spendingKey, PrfExpandCodes.SaplingOvk, expandOutput);
			Span<byte> ovk = stackalloc byte[32];
			expandOutput[..32].CopyTo(ovk);

			PRFexpand(spendingKey, PrfExpandCodes.SaplingDk, expandOutput);
			Span<byte> dk = stackalloc byte[32];
			expandOutput[..32].CopyTo(dk);

			return new ExtendedSpendingKey(new(ask, nsk, ovk), new(dk), chainCode, default, 0, 0, testNet);
		}
	}
}
