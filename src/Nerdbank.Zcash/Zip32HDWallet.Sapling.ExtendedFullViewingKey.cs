// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Numerics;
using Nerdbank.Zcash.Sapling;

namespace Nerdbank.Zcash;

public partial class Zip32HDWallet
{
	public partial class Sapling
	{
		/// <summary>
		/// The full viewing key, extended so it can be used to derive child keys.
		/// </summary>
		public class ExtendedFullViewingKey : IExtendedKey
		{
			/// <summary>
			/// Initializes a new instance of the <see cref="ExtendedFullViewingKey"/> class.
			/// </summary>
			/// <param name="spendingKey">The spending key from which to derive the full viewing key.</param>
			internal ExtendedFullViewingKey(ExtendedSpendingKey spendingKey)
			{
				this.Key = new(spendingKey.ExpandedSpendingKey, spendingKey.Dk, spendingKey.Network);
				this.ParentFullViewingKeyTag = spendingKey.ParentFullViewingKeyTag;
				this.ChainCode = spendingKey.ChainCode;
				this.ChildIndex = spendingKey.ChildIndex;
				this.Depth = spendingKey.Depth;
			}

			/// <summary>
			/// Initializes a new instance of the <see cref="ExtendedFullViewingKey"/> class.
			/// </summary>
			/// <param name="key">The full viewing key.</param>
			/// <param name="chainCode">The chain code.</param>
			/// <param name="parentFullViewingKeyTag">The tag from the full viewing key. Use the default value if not derived.</param>
			/// <param name="depth">The derivation depth of this key. Use 0 if there is no parent.</param>
			/// <param name="childIndex">The derivation number used to derive this key from its parent. Use 0 if there is no parent.</param>
			internal ExtendedFullViewingKey(DiversifiableFullViewingKey key, in ChainCode chainCode, in FullViewingKeyTag parentFullViewingKeyTag, byte depth, uint childIndex)
			{
				this.Key = key;
				this.ParentFullViewingKeyTag = parentFullViewingKeyTag;
				this.ChainCode = chainCode;
				this.ChildIndex = childIndex;
				this.Depth = depth;
			}

			/// <inheritdoc/>
			public FullViewingKeyFingerprint Fingerprint => GetFingerprint(this.Key);

			/// <inheritdoc/>
			public FullViewingKeyTag ParentFullViewingKeyTag { get; }

			/// <inheritdoc/>
			public ChainCode ChainCode { get; }

			/// <inheritdoc/>
			public uint ChildIndex { get; }

			/// <inheritdoc/>
			public byte Depth { get; }

			/// <inheritdoc/>
			public ZcashNetwork Network => this.Key.Network;

			/// <inheritdoc/>
			bool IKey.IsTestNet => this.Network != ZcashNetwork.MainNet;

			/// <summary>
			/// Gets the full viewing key.
			/// </summary>
			public DiversifiableFullViewingKey Key { get; }

			/// <summary>
			/// Gets the diversifier key.
			/// </summary>
			/// <value>A 32-byte buffer.</value>
			internal DiversifierKey Dk => this.Key.Dk;

			/// <inheritdoc cref="Cryptocurrencies.IExtendedKey.Derive(uint)"/>
			public ExtendedFullViewingKey Derive(uint childIndex)
			{
				Span<byte> selfAsBytes = stackalloc byte[169];
				Span<byte> childAsBytes = stackalloc byte[169];
				this.Encode(selfAsBytes);
				if (NativeMethods.DeriveSaplingChildFullViewingKey(selfAsBytes, childIndex, childAsBytes) != 0)
				{
					throw new InvalidKeyException();
				}

				return Decode(childAsBytes, this.Network);
			}

			/// <inheritdoc/>
			Cryptocurrencies.IExtendedKey Cryptocurrencies.IExtendedKey.Derive(uint childIndex) => this.Derive(childIndex);

			private static ExtendedFullViewingKey Decode(ReadOnlySpan<byte> encoded, ZcashNetwork network)
			{
				byte depth = encoded[0];
				FullViewingKeyTag parentFullViewingKeyTag = new(encoded[1..5]);
				uint childIndex = BitUtilities.ReadUInt32LE(encoded[5..9]);
				ChainCode chainCode = new(encoded[9..41]);
				FullViewingKey fvk = FullViewingKey.FromBytes(encoded[41..137], network);
				DiversifierKey dk = new(encoded[137..169]);
				DiversifiableFullViewingKey dfvk = new(fvk, dk, network);
				return new(dfvk, chainCode, parentFullViewingKeyTag, depth, childIndex);
			}

			/// <summary>
			/// Encodes the entire extended spending key.
			/// </summary>
			/// <param name="result">A buffer of at least 169 bytes in length.</param>
			/// <returns>The number of bytes written. Always 169.</returns>
			/// <remarks>
			/// This is designed to exactly match how rust encodes the extended full viewing key so we can exchange the data.
			/// </remarks>
			private int Encode(Span<byte> result)
			{
				int length = 0;
				result[length++] = this.Depth;
				length += this.ParentFullViewingKeyTag.Value.CopyToRetLength(result[length..]);
				length += BitUtilities.WriteLE(this.ChildIndex, result[length..]);
				length += this.ChainCode.Value.CopyToRetLength(result[length..]);
				length += this.Key.ToBytes(result[length..]);
				length += this.Dk.Value.CopyToRetLength(result[length..]);
				Assumes.True(length == 169);
				return length;
			}
		}
	}
}
