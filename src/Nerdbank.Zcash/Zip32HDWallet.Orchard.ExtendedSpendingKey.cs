// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash;

public partial class Zip32HDWallet
{
	public partial class Orchard
	{
		public class ExtendedSpendingKey : ExtendedKeyBase
		{
			private ExtendedFullViewingKey? fullViewingKey;

			internal ExtendedSpendingKey(in SpendingKey spendingKey, in ChainCode chainCode, in FullViewingKeyTag parentFullViewingKeyTag, byte depth, uint childNumber, bool testNet = false)
				: base(chainCode, parentFullViewingKeyTag, depth, childNumber, testNet)
			{
				this.SpendingKey = spendingKey;
			}

			public ExtendedFullViewingKey FullViewingKey => this.fullViewingKey ??= new(this);

			internal SpendingKey SpendingKey { get; }

			/// <inheritdoc/>
			public override ExtendedSpendingKey Derive(uint childNumber)
			{
				bool childIsHardened = (childNumber & Bip32HDWallet.HardenedBit) != 0;
				if (!childIsHardened)
				{
					throw new ArgumentException(Strings.OnlyHardenedChildKeysSupported, nameof(childNumber));
				}

				Span<byte> bytes = stackalloc byte[32 + 4];
				int bytesWritten = 0;
				bytesWritten += this.SpendingKey.Value.CopyToRetLength(bytes);
				bytesWritten += I2LEOSP(childNumber, bytes.Slice(bytesWritten, 4));
				Span<byte> i = stackalloc byte[64];
				PRFexpand(this.ChainCode.Value, PrfExpandCodes.OrchardZip32Child, bytes, i);
				Span<byte> spendingKey = i[0..32];
				ChainCode chainCode = new(i[32..]);

				SpendingKey key = new(spendingKey);
				return new ExtendedSpendingKey(
					key,
					chainCode,
					parentFullViewingKeyTag: this.FullViewingKey.Tag,
					depth: checked((byte)(this.Depth + 1)),
					childNumber,
					this.IsTestNet);
			}
		}
	}
}
