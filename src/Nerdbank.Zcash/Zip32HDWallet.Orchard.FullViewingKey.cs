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
			private readonly BigInteger ak;
			private readonly BigInteger nk;
			private readonly BigInteger rivk;

			internal FullViewingKey(BigInteger ak, BigInteger nk, BigInteger rivk)
			{
				this.ak = ak;
				this.nk = nk;
				this.rivk = rivk;
			}

			internal BigInteger Ak => this.ak;

			internal BigInteger Nk => this.nk;

			internal BigInteger Rivk => this.rivk;

			/// <summary>
			/// Creates a diversifier (the value of <see cref="OrchardReceiver.D"/>)
			/// for this viewing key.
			/// </summary>
			/// <param name="index">
			/// The index of the diversifier to generate, in the range of 0..(2^88 - 1).
			/// Every index produces a valid diversifier.
			/// The default diversifier is defined as to be 0.
			/// </param>
			/// <param name="d">Receives the diversifier. Exactly 11 bytes from this span will be initialized.</param>
			/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="index"/> is negative or excees the maximum supported value (2^88-1).</exception>
			/// <remarks>
			/// As specfied by <see href="https://zips.z.cash/zip-0032#orchard-diversifier-derivation">ZIP-0032 Orchard Diversifier Derivation</see>.
			/// </remarks>
			internal void GetDiversifier(BigInteger index, Span<byte> d)
			{
				Requires.Range(index >= 0 && index <= MaxDiversifierIndex, nameof(index));

				Span<byte> K = stackalloc byte[32];
				I2LEBSP(this.Rivk, K);

				Span<byte> B = stackalloc byte[64];
				int bBytesWritten = Repr_P(this.Ak, B);
				bBytesWritten += I2LEBSP(this.Nk, B.Slice(bBytesWritten, 32));

				Span<byte> t = stackalloc byte[65];
				t[0] = 0x82;
				LEBS2OSP(B, t[1..]);
				Span<byte> prfExpandOutput = stackalloc byte[64];
				PRFexpand(K, t, prfExpandOutput);
				Span<byte> dk = prfExpandOutput[..32];

				Span<byte> indexEncoded = stackalloc byte[11];
				I2LEBSP(index, indexEncoded);
				FF1AES256(dk, indexEncoded, d);
			}

			/// <summary>
			/// Gets the raw encoding.
			/// </summary>
			/// <param name="rawEncoding">Receives the raw encoding. Must be at least 96 bytes in length.</param>
			/// <returns>The number of bytes written to <paramref name="rawEncoding"/>. Always 96.</returns>
			/// <remarks>
			/// As specified in the <see href="https://zips.z.cash/protocol/protocol.pdf">Zcash protocol spec section 5.6.4.4</see>.
			/// </remarks>
			private int GetRawEncoding(Span<byte> rawEncoding)
			{
				int written = 0;
				written += I2LEOSP(this.Ak, rawEncoding[..32]);
				written += I2LEOSP(this.Nk, rawEncoding[32..64]);
				written += I2LEOSP(this.Rivk, rawEncoding[64..96]);
				return written;
			}

			/// <summary>
			/// Gets the fingerprint for the full viewing key.
			/// </summary>
			/// <param name="fingerprint">The buffer into which to write the fingerprint. Must be at least 32 bytes in length.</param>
			/// <returns>The number of bytes written to the <paramref name="fingerprint"/>. Always 32.</returns>
			private int GetFingerprint(Span<byte> fingerprint)
			{
				Span<byte> fvk = stackalloc byte[96];
				this.GetRawEncoding(fvk);
				return Blake2B.ComputeHash(fvk, fingerprint, new Blake2B.Config { Personalization = "ZcashOrchardFVFP"u8, OutputSizeInBytes = 32 });
			}
		}
	}
}
