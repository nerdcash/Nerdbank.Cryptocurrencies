// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Nerdbank.Zcash;

public partial class Zip32HDWallet
{
	public partial class Sapling
	{
		public class SpendingKey
		{
			private readonly BigInteger ask;
			private readonly BigInteger nsk;
			private readonly FixedArrays fixedArrays;

			public SpendingKey(BigInteger ask, BigInteger nsk, ReadOnlySpan<byte> ovk, ReadOnlySpan<byte> dk)
			{
				if (ovk.Length != 32)
				{
					throw new ArgumentException($"Length must be exactly 32, but was {ovk.Length}.", nameof(ovk));
				}

				if (dk.Length != 32)
				{
					throw new ArgumentException($"Length must be exactly 32, but was {dk.Length}.", nameof(dk));
				}

				if (ask == 0)
				{
					throw new ArgumentException(Strings.InvalidKey);
				}

				this.ask = ask;
				this.nsk = nsk;
				this.fixedArrays = new(ovk, dk);
			}

			internal BigInteger Ask => this.ask;

			internal BigInteger Nsk => this.nsk;

			internal ReadOnlySpan<byte> Ovk => this.fixedArrays.Ovk;

			internal ReadOnlySpan<byte> Dk => this.fixedArrays.Dk;

			/// <summary>
			/// Encodes an extended spending key to a buffer.
			/// </summary>
			/// <param name="result">The buffer to write the encoded key to.</param>
			/// <returns>The number of bytes written to <paramref name="result"/>. Always 128.</returns>
			internal int EncodeExtSKParts(Span<byte> result)
			{
				int bytesWritten = 0;

				I2LEOSP(this.ask, result[bytesWritten..32]);
				bytesWritten += 32;

				I2LEOSP(this.nsk, result[bytesWritten..32]);
				bytesWritten += 32;

				this.Ovk.CopyTo(result[bytesWritten..]);
				bytesWritten += this.Ovk.Length; // +32

				this.Dk.CopyTo(result[bytesWritten..]);
				bytesWritten += this.Dk.Length; // +32

				return bytesWritten;
			}

			private unsafe struct FixedArrays
			{
				private fixed byte ovk[32];
				private fixed byte dk[32];

				public FixedArrays(ReadOnlySpan<byte> ovk, ReadOnlySpan<byte> dk)
				{
					ovk.CopyToWithLengthCheck(this.OvkWritable);
					dk.CopyToWithLengthCheck(this.DkWritable);
				}

				internal ReadOnlySpan<byte> Ovk => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in this.ovk[0]), 32);

				internal ReadOnlySpan<byte> Dk => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in this.dk[0]), 32);

				private Span<byte> OvkWritable => MemoryMarshal.CreateSpan(ref this.ovk[0], 32);

				private Span<byte> DkWritable => MemoryMarshal.CreateSpan(ref this.dk[0], 32);
			}
		}
	}
}
