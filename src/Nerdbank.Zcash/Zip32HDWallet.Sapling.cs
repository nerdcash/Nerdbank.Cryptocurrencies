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
