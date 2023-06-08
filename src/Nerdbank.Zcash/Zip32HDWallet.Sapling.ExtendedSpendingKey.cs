// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Numerics;
using Org.BouncyCastle.Math.EC;

namespace Nerdbank.Zcash;

public partial class Zip32HDWallet
{
	public partial class Sapling
	{
		public class ExtendedSpendingKey : ExtendedKeyBase
		{
			internal ExtendedSpendingKey(SpendingKey key, ReadOnlySpan<byte> chainCode, ReadOnlySpan<byte> parentFullViewingKeyTag, byte depth, uint childNumber, bool isTestNet = false)
				: base(chainCode, parentFullViewingKeyTag, depth, childNumber, isTestNet)
			{
				if (chainCode.Length != 32)
				{
					throw new ArgumentException($"Length must be exactly 32, but was {chainCode.Length}.", nameof(chainCode));
				}

				this.SpendingKey = key;
			}

			public ExtendedFullViewingKey FullViewingKey { get; }

			public SpendingKey SpendingKey { get; }

			/// <summary>
			/// Derives a spending key from a given parent key.
			/// </summary>
			/// <param name="childNumber">The index of the derived child key.</param>
			/// <returns>The derived key.</returns>
			public override ExtendedSpendingKey Derive(uint childNumber)
			{
				bool childIsHardened = (childNumber & Bip32HDWallet.HardenedBit) != 0;
				Span<byte> i = stackalloc byte[64];
				if (childIsHardened)
				{
					Span<byte> bytes = stackalloc byte[133];
					bytes[0] = 0x11;
					int bytesWritten = 1;
					bytesWritten += this.SpendingKey.EncodeExtSKParts(bytes[bytesWritten..]);
					bytesWritten += I2LEOSP(childNumber, bytes.Slice(bytesWritten, 4));
					PRFexpand(this.ChainCode, bytes[..bytesWritten], i);
				}
				else
				{
					Span<byte> bytes = stackalloc byte[133];
					bytes[0] = 0x12;
					int bytesWritten = 1;
					bytesWritten += this.FullViewingKey.EncodeExtFVKParts(bytes[bytesWritten..]);
					bytesWritten += I2LEOSP(childNumber, bytes.Slice(bytesWritten, 4));
					PRFexpand(this.ChainCode, bytes[..bytesWritten], i);
				}

				Span<byte> il = i[0..32];
				Span<byte> ir = i[32..];
				Span<byte> expandOutput = stackalloc byte[64];

				PRFexpand(il, new(0x13), expandOutput);
				BigInteger ask = ToScalar(expandOutput);

				PRFexpand(il, new(0x14), expandOutput);
				BigInteger nsk = ToScalar(expandOutput);

				Span<byte> ovk = stackalloc byte[33];
				ovk[0] = 0x15;
				this.SpendingKey.Ovk.CopyTo(ovk[1..]);
				PRFexpand(il, ovk, expandOutput);
				expandOutput[..32].CopyTo(ovk);

				Span<byte> dk = stackalloc byte[33];
				dk[0] = 0x16;
				this.SpendingKey.Dk.CopyTo(dk[1..]);
				PRFexpand(il, dk, expandOutput);
				expandOutput[..32].CopyTo(dk);

				SpendingKey key = new(
					ask: BigInteger.Remainder(ask + this.SpendingKey.Ask, Curves.JubJub.Order),
					nsk: BigInteger.Remainder(nsk - this.SpendingKey.Nsk, Curves.JubJub.Order),
					ovk: ovk[..32],
					dk: dk[..32]);

				return new ExtendedSpendingKey(
					key,
					chainCode: ir,
					parentFullViewingKeyTag: this.FullViewingKey.Fingerprint[..4],
					depth: checked((byte)(this.Depth + 1)),
					childNumber: childNumber,
					isTestNet: this.IsTestNet);
			}
		}
	}
}
