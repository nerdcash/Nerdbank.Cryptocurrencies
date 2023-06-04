// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash;

public partial class Zip32HDWallet
{
	public partial class Orchard
	{
		public class ExtendedFullViewingKey : ExtendedKeyBase
		{
			public ExtendedFullViewingKey(FullViewingKey key, ReadOnlySpan<byte> chainCode, ReadOnlySpan<byte> parentFullViewingKeyTag, byte depth, uint childNumber, bool isTestNet = false)
				: base(chainCode, parentFullViewingKeyTag, depth, childNumber, isTestNet)
			{
				this.Key = key;
			}

			public FullViewingKey Key { get; }

			public override ReadOnlySpan<byte> Fingerprint => throw new NotImplementedException();

			public override ExtendedKeyBase Derive(uint childNumber)
			{
				throw new NotImplementedException();
			}
		}
	}
}
