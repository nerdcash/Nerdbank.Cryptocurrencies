// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Numerics;
using Org.BouncyCastle.Math.EC;

namespace Nerdbank.Zcash;

public partial class Zip32HDWallet
{
	public partial class Orchard
	{
		public class FullViewingKey
		{
			private readonly ECPoint ak;
			private readonly BigInteger nk;
			private readonly BigInteger rivk;

			internal FullViewingKey(ECPoint ak, BigInteger nk, BigInteger rivk)
			{
				this.ak = ak;
				this.nk = nk;
				this.rivk = rivk;
			}

			internal ECPoint Ak => this.ak;

			internal BigInteger Nk => this.nk;

			internal BigInteger Rivk => this.rivk;

			/// <summary>
			/// Encodes a diversifier (the value of <see cref="OrchardReceiver.D"/>)
			/// for a given <see cref="ExtendedFullViewingKey"/>.
			/// </summary>
			/// <param name="index">
			/// The diversifier index, in the range of 0..(2^88 - 1).
			/// Every index will produce a valid diversifier.
			/// The default diversifier is defined as the one with 0 for this value.
			/// </param>
			/// <param name="d">Receives the diversifier. Exactly 88 bytes from this span will be initialized.</param>
			internal void GetDiversifier(BigInteger index, Span<byte> d)
			{
				Span<byte> k = stackalloc byte[256 / 8];
				I2LEBSP(this.Rivk, k);
				Span<byte> b = stackalloc byte[64]; // length?
				int bytesWritten = Repr(this.Ak, b);
				bytesWritten += I2LEBSP(this.Nk, b[bytesWritten..]);

				Span<byte> prfExpandInput = stackalloc byte[32];
				prfExpandInput[0] = 0x82;
				LEBS2OSP(b[..bytesWritten], prfExpandInput[1..]);
				Span<byte> prfExpandOutput = stackalloc byte[64];
				PRFexpand(k, prfExpandInput[..(1 + bytesWritten)], prfExpandOutput);
				Span<byte> dk = prfExpandOutput[..32];

				Span<byte> indexAsBytes = stackalloc byte[88];
				I2LEBSP(index, indexAsBytes);
				FF1AES256(dk, indexAsBytes, d);
			}
		}
	}
}
