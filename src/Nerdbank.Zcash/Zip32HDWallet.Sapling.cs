// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Bitcoin;
using Nerdbank.Zcash.Sapling;
using static Nerdbank.Bitcoin.Bip32HDWallet;

namespace Nerdbank.Zcash;

public partial class Zip32HDWallet
{
	/// <summary>
	/// Contains types and methods related to the Sapling pool.
	/// </summary>
	public static partial class Sapling
	{
		/// <inheritdoc cref="Create(ReadOnlySpan{byte}, ZcashNetwork)" path="/summary"/>
		/// <inheritdoc cref="Create(ReadOnlySpan{byte}, ZcashNetwork)" path="/returns"/>
		/// <param name="mnemonic">The mnemonic phrase from which to generate the master key.</param>
		/// <param name="network"><inheritdoc cref="Create(ReadOnlySpan{byte}, ZcashNetwork)" path="/param[@name='network']"/></param>
		public static ExtendedSpendingKey Create(Bip39Mnemonic mnemonic, ZcashNetwork network) => Create(Requires.NotNull(mnemonic).Seed, network);

		/// <summary>
		/// Creates a master key for the Sapling pool.
		/// </summary>
		/// <param name="seed">
		/// The seed byte sequence, which MUST be at least 32 and at most 252 bytes.
		/// This seed SHOULD be generated from entropy of at least <see cref="MinimumEntropyLengthInBits"/> in length to meet Zcash security modeling.
		/// </param>
		/// <param name="network">The network this key should be used with.</param>
		/// <returns>The master extended spending key.</returns>
		public static ExtendedSpendingKey Create(ReadOnlySpan<byte> seed, ZcashNetwork network)
		{
			ThrowIfSeedHasDisallowedSize(seed);
			Span<byte> blakeOutput = stackalloc byte[64]; // 512 bits
			Blake2B.ComputeHash(seed, blakeOutput, new Blake2B.Config { Personalization = "ZcashIP32Sapling"u8, OutputSizeInBytes = blakeOutput.Length });
			ReadOnlySpan<byte> spendingKey = blakeOutput[..32];
			ref readonly ChainCode chainCode = ref ChainCode.From(blakeOutput[32..]);

			return new ExtendedSpendingKey(new(spendingKey, network), chainCode, default, 0, 0)
			{
				DerivationPath = Bip32KeyPath.Root,
			};
		}

		/// <summary>
		/// Gets the fingerprint for this key.
		/// </summary>
		/// <param name="fullViewingKey">The full viewing key for which to produce a fingerprint.</param>
		/// <returns>The fingerprint.</returns>
		internal static FullViewingKeyFingerprint GetFingerprint(FullViewingKey fullViewingKey)
		{
			Requires.NotNull(fullViewingKey);

			FullViewingKeyFingerprint fingerprint = default;
			Bytes96 rawEncoding = fullViewingKey.ToBytes();
			Blake2B.ComputeHash(rawEncoding, fingerprint, new Blake2B.Config { Personalization = "ZcashSaplingFVFP"u8, OutputSizeInBytes = 32 });
			return fingerprint;
		}
	}
}
