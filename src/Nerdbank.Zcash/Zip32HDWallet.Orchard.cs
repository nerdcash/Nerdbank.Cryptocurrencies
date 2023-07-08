// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Zcash.Orchard;

namespace Nerdbank.Zcash;

public partial class Zip32HDWallet
{
	/// <summary>
	/// Contains types and methods related to the Orchard pool.
	/// </summary>
	public static partial class Orchard
	{
		/// <inheritdoc cref="Create(ReadOnlySpan{byte}, bool)"/>
		/// <param name="mnemonic">The mnemonic phrase from which to generate the master key.</param>
		/// <param name="testNet"><inheritdoc cref="Create(ReadOnlySpan{byte}, bool)" path="/param[@name='testNet']"/></param>
		public static ExtendedSpendingKey Create(Bip39Mnemonic mnemonic, bool testNet = false) => Create(Requires.NotNull(mnemonic).Seed, testNet);

		/// <summary>
		/// Creates a master key for the Orchard pool.
		/// </summary>
		/// <param name="seed">The seed for use to generate the master key. A given seed will always produce the same master key.</param>
		/// <param name="testNet"><see langword="true" /> to create a key for use on the Zcash testnet; <see langword="false"/> otherwise.</param>
		/// <returns>A master extended spending key.</returns>
		public static ExtendedSpendingKey Create(ReadOnlySpan<byte> seed, bool testNet = false)
		{
			// Rust: assert!(seed.len() >= 32 && seed.len() <= 252);
			Span<byte> blakeOutput = stackalloc byte[64]; // 512 bits
			Blake2B.ComputeHash(seed, blakeOutput, new Blake2B.Config { Personalization = "ZcashIP32Orchard"u8, OutputSizeInBytes = blakeOutput.Length });

			SpendingKey spendingKey = new(blakeOutput[..32]);
			ChainCode chainCode = new(blakeOutput[32..]);

			return new ExtendedSpendingKey(
				spendingKey,
				chainCode,
				parentFullViewingKeyTag: default,
				depth: 0,
				childIndex: 0,
				testNet);
		}

		/// <summary>
		/// Gets the fingerprint for this key.
		/// </summary>
		public static FullViewingKeyFingerprint GetFingerprint(FullViewingKey fullViewingKey)
		{
			Requires.NotNull(fullViewingKey);

			Span<byte> output = stackalloc byte[32];
			Blake2B.ComputeHash(fullViewingKey.RawEncoding, output, new Blake2B.Config { Personalization = "ZcashOrchardFVFP"u8, OutputSizeInBytes = 32 });
			return new(output);
		}
	}
}
