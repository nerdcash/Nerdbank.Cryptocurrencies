// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Numerics;

namespace Nerdbank.Zcash;

public partial class Zip32HDWallet
{
	public partial class Orchard
	{
		/// <summary>
		/// A full viewing key, extended to allow deriving child keys.
		/// </summary>
		public class ExtendedFullViewingKey : ExtendedKeyBase
		{
			/// <summary>
			/// Initializes a new instance of the <see cref="ExtendedFullViewingKey"/> class.
			/// </summary>
			/// <param name="spendingKey">The spending key from which to derive the viewing key.</param>
			internal ExtendedFullViewingKey(ExtendedSpendingKey spendingKey)
				: base(spendingKey.ChainCode, spendingKey.ParentFullViewingKeyTag, spendingKey.Depth, spendingKey.ChildNumber, spendingKey.IsTestNet)
			{
				this.Key = new(spendingKey.SpendingKey);
			}

			/// <summary>
			/// Initializes a new instance of the <see cref="ExtendedFullViewingKey"/> class.
			/// </summary>
			/// <param name="key">The full viewing key.</param>
			/// <param name="chainCode"><inheritdoc cref="ExtendedKeyBase(in ChainCode, in FullViewingKeyTag, byte, uint, bool)" path="/param[@name='chainCode']"/></param>
			/// <param name="parentFullViewingKeyTag"><inheritdoc cref="ExtendedKeyBase(in ChainCode, in FullViewingKeyTag, byte, uint, bool)" path="/param[@name='parentFullViewingKeyTag']"/></param>
			/// <param name="depth"><inheritdoc cref="ExtendedKeyBase(in ChainCode, in FullViewingKeyTag, byte, uint, bool)" path="/param[@name='depth']"/></param>
			/// <param name="childNumber"><inheritdoc cref="ExtendedKeyBase(in ChainCode, in FullViewingKeyTag, byte, uint, bool)" path="/param[@name='childNumber']"/></param>
			/// <param name="isTestNet"><inheritdoc cref="ExtendedKeyBase(in ChainCode, in FullViewingKeyTag, byte, uint, bool)" path="/param[@name='isTestNet']"/></param>
			internal ExtendedFullViewingKey(FullViewingKey key, in ChainCode chainCode, in FullViewingKeyTag parentFullViewingKeyTag, byte depth, uint childNumber, bool isTestNet = false)
				: base(chainCode, parentFullViewingKeyTag, depth, childNumber, isTestNet)
			{
				this.Key = key;
			}

			/// <summary>
			/// Gets the full viewing key itself.
			/// </summary>
			public FullViewingKey Key { get; }

			/// <inheritdoc cref="FullViewingKey.Fingerprint"/>
			/// <remarks>
			/// Extended keys can be identified by the Hash160 (RIPEMD160 after SHA256) of the serialized ECDSA public key K, ignoring the chain code.
			/// This corresponds exactly to the data used in traditional Bitcoin addresses.
			/// It is not advised to represent this data in base58 format though, as it may be interpreted as an address that way
			/// (and wallet software is not required to accept payment to the chain key itself).
			/// </remarks>
			public FullViewingKeyFingerprint Fingerprint => this.Key.Fingerprint;

			/// <inheritdoc cref="FullViewingKey.Tag"/>
			public FullViewingKeyTag Tag => this.Key.Tag;

			/// <inheritdoc/>
			public override ExtendedKeyBase Derive(uint childNumber)
			{
				// Orchard does not define child key derivation based on full viewing keys.
				throw new NotSupportedException();
			}
		}
	}
}
