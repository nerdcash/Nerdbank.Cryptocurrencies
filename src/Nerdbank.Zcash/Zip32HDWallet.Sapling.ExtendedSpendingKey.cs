// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Numerics;

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
					Span<byte> bytes = stackalloc byte[132];
					int bytesWritten = 0;
					bytesWritten += this.SpendingKey.EncodeExtSKParts(bytes[bytesWritten..]);
					bytesWritten += I2LEOSP(childNumber, bytes.Slice(bytesWritten, 4));
					PRFexpand(this.ChainCode, PrfExpandCodes.SaplingExtSK, bytes[..bytesWritten], i);
				}
				else
				{
					Span<byte> bytes = stackalloc byte[132];
					int bytesWritten = 0;
					bytesWritten += this.FullViewingKey.EncodeExtFVKParts(bytes[bytesWritten..]);
					bytesWritten += I2LEOSP(childNumber, bytes.Slice(bytesWritten, 4));
					PRFexpand(this.ChainCode, PrfExpandCodes.SaplingExtFVK, bytes[..bytesWritten], i);
				}

				Span<byte> il = i[0..32];
				Span<byte> ir = i[32..];
				Span<byte> expandOutput = stackalloc byte[64];

				PRFexpand(il, PrfExpandCodes.SaplingAskDerive, expandOutput);
				BigInteger ask = ToScalar(expandOutput);

				PRFexpand(il, PrfExpandCodes.SaplingNskDerive, expandOutput);
				BigInteger nsk = ToScalar(expandOutput);

				Span<byte> ovk = stackalloc byte[32];
				PRFexpand(il, PrfExpandCodes.SaplingOvkDerive, this.SpendingKey.Ovk, expandOutput);
				expandOutput[..32].CopyTo(ovk);

				PRFexpand(il, PrfExpandCodes.SaplingDkDerive, this.SpendingKey.Dk, expandOutput);
				Span<byte> dk = stackalloc byte[32];
				expandOutput[..32].CopyTo(dk);

				SpendingKey key = new(
					ask: BigInteger.Remainder(ask + this.SpendingKey.Ask, Curves.JubJub.Order),
					nsk: BigInteger.Remainder(nsk - this.SpendingKey.Nsk, Curves.JubJub.Order),
					ovk: ovk,
					dk: dk);

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
