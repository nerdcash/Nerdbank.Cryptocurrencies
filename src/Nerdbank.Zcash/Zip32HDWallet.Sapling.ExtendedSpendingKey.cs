// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Numerics;

namespace Nerdbank.Zcash;

public partial class Zip32HDWallet
{
	public partial class Sapling
	{
		/// <summary>
		/// The extended spending key.
		/// </summary>
		public class ExtendedSpendingKey : IExtendedKey
		{
			private ExtendedFullViewingKey? fullViewingKey;

			/// <summary>
			/// Initializes a new instance of the <see cref="ExtendedSpendingKey"/> class.
			/// </summary>
			/// <param name="key">The spending key's ask, nsk and ovk components.</param>
			/// <param name="dk">The diversifier key.</param>
			/// <param name="chainCode">The chain code.</param>
			/// <param name="parentFullViewingKeyTag">The tag from the full viewing key. Use the default value if not derived.</param>
			/// <param name="depth">The derivation depth of this key. Use 0 if there is no parent.</param>
			/// <param name="childNumber">The derivation number used to derive this key from its parent. Use 0 if there is no parent.</param>
			/// <param name="isTestNet">A value indicating whether this key is to be used on a testnet.</param>
			internal ExtendedSpendingKey(in ExpandedSpendingKey key, in DiversifierKey dk, in ChainCode chainCode, in FullViewingKeyTag parentFullViewingKeyTag, byte depth, uint childNumber, bool isTestNet = false)
			{
				this.ExpandedSpendingKey = key;
				this.Dk = dk;
				this.ChainCode = chainCode;
				this.ParentFullViewingKeyTag = parentFullViewingKeyTag;
				this.Depth = depth;
				this.ChildNumber = childNumber;
				this.IsTestNet = isTestNet;
			}

			/// <summary>
			/// Gets the extended full viewing key.
			/// </summary>
			public ExtendedFullViewingKey FullViewingKey => this.fullViewingKey ??= new(this);

			/// <inheritdoc/>
			public FullViewingKeyTag ParentFullViewingKeyTag { get; }

			/// <inheritdoc/>
			public ChainCode ChainCode { get; }

			/// <inheritdoc/>
			public uint ChildNumber { get; }

			/// <inheritdoc/>
			public byte Depth { get; }

			/// <inheritdoc/>
			public bool IsTestNet { get; }

			/// <summary>
			/// Gets the expanded spending key (one that has ask, nsk, and ovk derived from the raw 32-byte spending key).
			/// </summary>
			internal ExpandedSpendingKey ExpandedSpendingKey { get; }

			/// <summary>
			/// Gets the diversifier key.
			/// </summary>
			internal DiversifierKey Dk { get; }

			/// <summary>
			/// Derives a spending key from a given parent key.
			/// </summary>
			/// <param name="childNumber">The index of the derived child key.</param>
			/// <returns>The derived key.</returns>
			public ExtendedSpendingKey Derive(uint childNumber)
			{
				Span<byte> selfAsBytes = stackalloc byte[169];
				Span<byte> childAsBytes = stackalloc byte[169];
				this.Encode(selfAsBytes);
				if (NativeMethods.DeriveSaplingChild(selfAsBytes, childNumber, childAsBytes) != 0)
				{
					throw new InvalidKeyException();
				}

				return Decode(childAsBytes, this.IsTestNet);
			}

			/// <inheritdoc/>
			Cryptocurrencies.IExtendedKey Cryptocurrencies.IExtendedKey.Derive(uint childNumber) => this.Derive(childNumber);

			private static ExtendedSpendingKey Decode(ReadOnlySpan<byte> encoded, bool isTestNet)
			{
				byte depth = encoded[0];
				FullViewingKeyTag parentFullViewingKeyTag = new(encoded[1..5]);
				uint childNumber = BitUtilities.ReadUInt32LE(encoded[5..9]);
				ChainCode chainCode = new(encoded[9..41]);
				ExpandedSpendingKey expsk = ExpandedSpendingKey.FromBytes(encoded[41..137]);
				DiversifierKey dk = new(encoded[137..169]);
				return new(expsk, dk, chainCode, parentFullViewingKeyTag, depth, childNumber, isTestNet);
			}

			/// <summary>
			/// Encodes the entire extended spending key.
			/// </summary>
			/// <param name="result">A buffer of at least 169 bytes in length.</param>
			/// <returns>The number of bytes written. Always 169.</returns>
			/// <remarks>
			/// This is designed to exactly match how rust encodes the extended spending key so we can exchange the data.
			/// </remarks>
			private int Encode(Span<byte> result)
			{
				int length = 0;
				result[length++] = this.Depth;
				length += this.ParentFullViewingKeyTag.Value.CopyToRetLength(result[length..]);
				length += BitUtilities.WriteLE(this.ChildNumber, result[length..]);
				length += this.ChainCode.Value.CopyToRetLength(result[length..]);
				length += this.ExpandedSpendingKey.ToBytes(result[length..]);
				length += this.Dk.Value.CopyToRetLength(result[length..]);
				Assumes.True(length == 169);
				return length;
			}
		}
	}
}
