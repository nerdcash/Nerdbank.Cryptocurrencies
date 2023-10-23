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
		/// <inheritdoc cref="Create(ReadOnlySpan{byte}, ZcashNetwork)"/>
		/// <param name="mnemonic">The mnemonic phrase from which to generate the master key.</param>
		/// <param name="network"><inheritdoc cref="Create(ReadOnlySpan{byte}, ZcashNetwork)" path="/param[@name='network']"/></param>
		public static ExtendedSpendingKey Create(Bip39Mnemonic mnemonic, ZcashNetwork network) => Create(ThrowIfEntropyTooShort(Requires.NotNull(mnemonic)).Seed, network);

		/// <summary>
		/// Creates a master key for the Orchard pool.
		/// </summary>
		/// <param name="seed">
		/// The seed for use to generate the master key. A given seed will always produce the same master key.
		/// This seed SHOULD be generated from entropy of at least <see cref="MinimumEntropyLengthInBits"/> in length to meet Zcash security modeling.
		/// </param>
		/// <param name="network">The network this key should be used with.</param>
		/// <returns>A master extended spending key.</returns>
		public static ExtendedSpendingKey Create(ReadOnlySpan<byte> seed, ZcashNetwork network)
		{
			ThrowIfSeedHasDisallowedSize(seed);
			Span<byte> blakeOutput = stackalloc byte[64]; // 512 bits
			Blake2B.ComputeHash(seed, blakeOutput, new Blake2B.Config { Personalization = "ZcashIP32Orchard"u8, OutputSizeInBytes = blakeOutput.Length });

			SpendingKey spendingKey = new(blakeOutput[..32], network);
			ChainCode chainCode = new(blakeOutput[32..]);

			return new ExtendedSpendingKey(
				spendingKey,
				chainCode,
				parentFullViewingKeyTag: default,
				depth: 0,
				childIndex: 0)
			{
				DerivationPath = Bip32HDWallet.KeyPath.Root,
			};
		}

		/// <summary>
		/// Gets the fingerprint for this key.
		/// </summary>
		/// <param name="fullViewingKey">The full viewing key.</param>
		/// <returns>The viewing key's fingerprint.</returns>
		internal static FullViewingKeyFingerprint GetFingerprint(FullViewingKey fullViewingKey)
		{
			Requires.NotNull(fullViewingKey);

			Span<byte> output = stackalloc byte[32];
			Blake2B.ComputeHash(fullViewingKey.RawEncoding, output, new Blake2B.Config { Personalization = "ZcashOrchardFVFP"u8, OutputSizeInBytes = 32 });
			return new(output);
		}
	}
}
