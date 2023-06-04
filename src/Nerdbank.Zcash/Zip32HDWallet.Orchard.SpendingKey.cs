// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Nerdbank.Zcash;

public partial class Zip32HDWallet
{
	public partial class Orchard
	{
		public class SpendingKey
		{
			private readonly FixedArrays fixedArrays;

			public SpendingKey(ReadOnlySpan<byte> spendingKey)
			{
				this.fixedArrays = new(spendingKey);
			}

			/// <summary>
			/// Gets the spending key. Always 32 bytes in length.
			/// </summary>
			internal ReadOnlySpan<byte> Sk => this.fixedArrays.Sk;

			private unsafe struct FixedArrays
			{
				/// <summary>
				/// Orchard spending key.
				/// </summary>
				private fixed byte sk[32];

				internal FixedArrays(ReadOnlySpan<byte> spendingKey)
				{
					spendingKey.CopyToWithLengthCheck(this.SkWritable);
				}

				internal readonly ReadOnlySpan<byte> Sk => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in this.sk[0]), 32);

				private Span<byte> SkWritable => MemoryMarshal.CreateSpan(ref this.sk[0], 32);
			}
		}
	}
}
