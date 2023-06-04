// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash;

public partial class Zip32HDWallet
{
	public partial class Orchard
	{
		public class ExtendedSpendingKey : ExtendedKeyBase
		{
			public ExtendedSpendingKey(SpendingKey spendingKey, ReadOnlySpan<byte> chainCode, ReadOnlySpan<byte> parentFullViewingKeyTag, byte depth, uint childNumber, bool isTestNet = false)
				: base(chainCode, parentFullViewingKeyTag, depth, childNumber, isTestNet)
			{
				this.SpendingKey = spendingKey;
			}

			public ExtendedFullViewingKey FullViewingKey { get; }

			public override ReadOnlySpan<byte> Fingerprint => throw new NotImplementedException();

			internal SpendingKey SpendingKey { get; }

			public override ExtendedSpendingKey Derive(uint childNumber)
			{
				bool childIsHardened = (childNumber & Bip32HDWallet.HardenedBit) != 0;
				if (!childIsHardened)
				{
					throw new ArgumentException(Strings.OnlyHardenedChildKeysSupported, nameof(childNumber));
				}

				Span<byte> bytes = stackalloc byte[1 + 32 + 4];
				bytes[0] = 0x81;
				int bytesWritten = 1;
				this.SpendingKey.Sk.CopyTo(bytes[bytesWritten..]);
				bytesWritten += this.SpendingKey.Sk.Length;
				bytesWritten += I2LEOSP(childNumber, bytes.Slice(bytesWritten, 4));
				Span<byte> i = stackalloc byte[64];
				PRFexpand(this.ChainCode, bytes[..bytesWritten], i);
				Span<byte> spendingKey = i[0..32];
				Span<byte> chainCode = i[32..];

				SpendingKey key = new(spendingKey);
				return new ExtendedSpendingKey(
					key,
					chainCode,
					parentFullViewingKeyTag: this.FullViewingKey.Fingerprint[..4],
					depth: checked((byte)(this.Depth + 1)),
					childNumber,
					this.IsTestNet);
			}
		}
	}
}
