// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Org.BouncyCastle.Math.EC;

namespace Nerdbank.Zcash;

public partial class Zip32HDWallet
{

	public partial class Sapling
	{
		public class FullViewingKey
		{
			private readonly ECPoint ak;
			private readonly ECPoint nk;
			private readonly FixedArrays fixedArrays;

			internal FullViewingKey(ECPoint ak, ECPoint nk, ReadOnlySpan<byte> ovk)
			{
				if (!ak.IsValid()) // TODO: Does this include a zero point check?
				{
					throw new ArgumentException(Strings.InvalidKey);
				}

				this.ak = ak;
				this.nk = nk;
				this.fixedArrays = new(ovk);
			}

			internal ECPoint Ak => this.ak;

			internal ECPoint Nk => this.nk;

			/// <summary>
			/// Gets the Ovk value.
			/// </summary>
			/// <value>A 32-byte buffer.</value>
			internal ReadOnlySpan<byte> Ovk => this.fixedArrays.Ovk;

			/// <summary>
			/// Gets the raw encoding.
			/// </summary>
			/// <param name="rawEncoding">Receives the raw encoding. Must be at least 96 bytes in length.</param>
			/// <returns>The number of bytes written to <paramref name="rawEncoding"/>. Always 96.</returns>
			/// <remarks>
			/// As specified in the <see href="https://zips.z.cash/protocol/protocol.pdf">Zcash protocol spec section 5.6.3.3</see>.
			/// </remarks>
			private int GetRawEncoding(Span<byte> rawEncoding)
			{
				Span<byte> reprOutput = stackalloc byte[32];
				int written = 0;

				Repr_J(this.Ak, reprOutput);
				written += LEBS2OSP(reprOutput, rawEncoding[..32]);

				Repr_J(this.Nk, reprOutput);
				written += LEBS2OSP(reprOutput[..written], rawEncoding[32..64]);

				written += this.Ovk.CopyToRetLength(rawEncoding[64..]);

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
				return Blake2B.ComputeHash(fvk, fingerprint, new Blake2B.Config { Personalization = "ZcashSaplingFVFP"u8, OutputSizeInBytes = 32 });
			}

			private unsafe struct FixedArrays
			{
				private fixed byte ovk[32];

				internal FixedArrays(ReadOnlySpan<byte> ovk)
				{
					ovk.CopyToWithLengthCheck(this.OvkWritable);
				}

				internal readonly ReadOnlySpan<byte> Ovk => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in this.ovk[0]), 32);

				internal Span<byte> OvkWritable => MemoryMarshal.CreateSpan(ref this.ovk[0], 32);
			}
		}
	}
}
