// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Numerics;

namespace Nerdbank.Zcash;

public partial class Zip32HDWallet
{
	public partial class Orchard
	{
		public class ExtendedFullViewingKey : ExtendedKeyBase
		{
			internal ExtendedFullViewingKey(FullViewingKey key, ReadOnlySpan<byte> chainCode, ReadOnlySpan<byte> parentFullViewingKeyTag, byte depth, uint childNumber, bool isTestNet = false)
				: base(chainCode, parentFullViewingKeyTag, depth, childNumber, isTestNet)
			{
				this.Key = key;
			}

			public FullViewingKey Key { get; }

			/// <summary>
			/// Gets the fingerprint for this key.
			/// </summary>
			/// <remarks>
			/// Extended keys can be identified by the Hash160 (RIPEMD160 after SHA256) of the serialized ECDSA public key K, ignoring the chain code.
			/// This corresponds exactly to the data used in traditional Bitcoin addresses.
			/// It is not advised to represent this data in base58 format though, as it may be interpreted as an address that way
			/// (and wallet software is not required to accept payment to the chain key itself).
			/// </remarks>
			public ReadOnlySpan<byte> Fingerprint => throw new NotImplementedException();

			public override ExtendedKeyBase Derive(uint childNumber)
			{
				throw new NotImplementedException();
			}
		}
	}
}
