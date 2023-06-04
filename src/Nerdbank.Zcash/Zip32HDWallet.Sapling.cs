// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Numerics;
using Org.BouncyCastle.Math.EC;

namespace Nerdbank.Zcash;

public partial class Zip32HDWallet
{
	/// <summary>
	/// The "Randomness Beacon".
	/// </summary>
	/// <remarks>
	/// The value for this is defined in <see href="https://zips.z.cash/protocol/protocol.pdf">the Zcash protocol</see> §5.9.
	/// </remarks>
	internal static readonly BigInteger URS = BigInteger.Parse("096b36a5804bfacef1691e173c366a47ff5ba84a44f26ddd7e8d9f79d5b42df0", System.Globalization.NumberStyles.HexNumber);

	public static partial class Sapling
	{
		internal static readonly ECPoint G_Sapling = Curves.JubJub.FindGroupHash("Zcash_G_", string.Empty);

		internal static readonly ECPoint H_Sapling = Curves.JubJub.FindGroupHash("Zcash_H_", string.Empty);

		/// <summary>
		/// Generates a master extended spending key.
		/// </summary>
		/// <param name="s">The seed byte sequence, which MUST be at least 32 and at most 252 bytes.</param>
		/// <param name="isTestNet"><see langword="true" /> when the generated key will be used to interact with the zcash testnet; <see langword="false" /> otherwise.</param>
		/// <returns>The master extended spending key.</returns>
		internal static ExtendedSpendingKey GenerateMasterKey(ReadOnlySpan<byte> s, bool isTestNet = false)
		{
			Span<byte> blakeOutput = stackalloc byte[64]; // 512 bits
			Blake2B.ComputeHash(s, blakeOutput, new Blake2B.Config { Personalization = "ZcashIP32Sapling"u8, OutputSizeInBytes = blakeOutput.Length });
			Span<byte> spendingKey = blakeOutput[..32];
			Span<byte> chainCode = blakeOutput[32..];

			Span<byte> expandOutput = stackalloc byte[64];
			PRFexpand(spendingKey, new(0x00), expandOutput);
			BigInteger ask = ToScalar(expandOutput);

			PRFexpand(spendingKey, new(0x01), expandOutput);
			BigInteger nsk = ToScalar(expandOutput);

			PRFexpand(spendingKey, new(0x02), expandOutput);
			Span<byte> ovk = stackalloc byte[32];
			expandOutput[..32].CopyTo(ovk);

			PRFexpand(spendingKey, new(0x10), expandOutput);
			Span<byte> dk = stackalloc byte[32];
			expandOutput[..32].CopyTo(dk);

			return new ExtendedSpendingKey(new(ask, nsk, ovk, dk), chainCode, default, 0, 0, isTestNet);
		}

		private static BigInteger ToScalar(ReadOnlySpan<byte> x)
		{
			return BigInteger.Remainder(LEOS2IP(x), Curves.JubJub.Order);
		}

		/// <summary>
		/// Encodes a diversifier (the value of <see cref="SaplingReceiver.D"/>)
		/// for a given diversifier key (the value of <see cref="ExtendedFullViewingKey.Dk"/>).
		/// </summary>
		/// <param name="dk">A 32-byte buffer containing the diversifier key.</param>
		/// <param name="index">
		/// The diversifier index, in the range of 0..(2^88 - 1).
		/// Not every index will produce a valid diversifier. About half will fail.
		/// The default diversifier is defined as the smallest non-negative index that produces a valid diversifier.
		/// </param>
		/// <param name="d">Receives the diversifier. Exactly 88 bytes from this span will be initialized.</param>
		/// <returns>
		/// <see langword="true"/> if a valid diversifier could be produced with the given <paramref name="index"/>.
		/// <see langword="false"/> if the caller should retry with the next higher index.
		/// </returns>
		internal static bool TryGetDiversifier(ReadOnlySpan<byte> dk, BigInteger index, Span<byte> d)
		{
			Span<byte> indexAsBytes = stackalloc byte[88];
			I2LEBSP(index, indexAsBytes);
			FF1AES256(dk, indexAsBytes, d);
			return !DiversifyHash(indexAsBytes).IsInfinity;
		}

		/// <summary>
		/// Maps a diversifier to a base point on the JubJub elliptic curve, or to ⊥ if the diversifier is invalid.
		/// </summary>
		/// <param name="d">The diversifier.</param>
		/// <returns>A point on the JubJub elliptic curve.</returns>
		private static ECPoint DiversifyHash(ReadOnlySpan<byte> d)
		{
			throw new NotImplementedException();
		}
	}
}
