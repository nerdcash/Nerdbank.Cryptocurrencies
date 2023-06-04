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

			public FullViewingKey(ECPoint ak, ECPoint nk, ReadOnlySpan<byte> ovk, ReadOnlySpan<byte> dk)
			{
				if (!ak.IsValid()) // TODO: Does this include a zero point check?
				{
					throw new ArgumentException(Strings.InvalidKey);
				}

				this.ak = ak;
				this.nk = nk;
				this.fixedArrays = new(ovk, dk);
			}

			internal ECPoint Ak => this.ak;

			internal ECPoint Nk => this.nk;

			internal ReadOnlySpan<byte> Ovk => this.fixedArrays.Ovk;

			internal ReadOnlySpan<byte> Dk => this.fixedArrays.Dk;

			internal int EncodeExtFVKParts(Span<byte> result)
			{
				// TODO: implement this.
				// This requires the EC function: reprJ
				throw new NotImplementedException();
			}

			private unsafe struct FixedArrays
			{
				private fixed byte ovk[32];
				private fixed byte dk[32];

				internal FixedArrays(ReadOnlySpan<byte> ovk, ReadOnlySpan<byte> dk)
				{
					ovk.CopyToWithLengthCheck(this.OvkWritable);
					dk.CopyToWithLengthCheck(this.DkWritable);
				}

				internal readonly ReadOnlySpan<byte> Ovk => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in this.ovk[0]), 32);

				internal Span<byte> OvkWritable => MemoryMarshal.CreateSpan(ref this.ovk[0], 32);

				internal readonly ReadOnlySpan<byte> Dk => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in this.dk[0]), 32);

				internal Span<byte> DkWritable => MemoryMarshal.CreateSpan(ref this.dk[0], 32);
			}
		}
	}
}
