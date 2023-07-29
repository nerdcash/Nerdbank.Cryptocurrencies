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
		/// The extended spending key.
		/// </summary>
		[DebuggerDisplay($"{{{nameof(DefaultAddress)},nq}}")]
		public class ExtendedSpendingKey : IExtendedKey, IEquatable<ExtendedSpendingKey>
		{
			private const string Bech32MainNetworkHRP = "secret-extended-key-main";
			private const string Bech32TestNetworkHRP = "secret-extended-key-test";

			private ExtendedFullViewingKey? fullViewingKey;

			/// <summary>
			/// Initializes a new instance of the <see cref="ExtendedSpendingKey"/> class.
			/// </summary>
			/// <param name="key">The spending key's ask, nsk and ovk components.</param>
			/// <param name="dk">The diversifier key.</param>
			/// <param name="chainCode">The chain code.</param>
			/// <param name="parentFullViewingKeyTag">The tag from the full viewing key. Use the default value if not derived.</param>
			/// <param name="depth">The derivation depth of this key. Use 0 if there is no parent.</param>
			/// <param name="childIndex">The derivation number used to derive this key from its parent. Use 0 if there is no parent.</param>
			/// <param name="network">The network this key should be used with.</param>
			internal ExtendedSpendingKey(in ExpandedSpendingKey key, in DiversifierKey dk, in ChainCode chainCode, in FullViewingKeyTag parentFullViewingKeyTag, byte depth, uint childIndex, ZcashNetwork network = ZcashNetwork.MainNet)
			{
				this.ExpandedSpendingKey = key;
				this.Dk = dk;
				this.ChainCode = chainCode;
				this.ParentFullViewingKeyTag = parentFullViewingKeyTag;
				this.Depth = depth;
				this.ChildIndex = childIndex;
				this.Network = network;
			}

			/// <summary>
			/// Gets the extended full viewing key.
			/// </summary>
			public ExtendedFullViewingKey FullViewingKey => this.fullViewingKey ??= new(this);

			/// <inheritdoc/>
			public FullViewingKeyFingerprint Fingerprint => GetFingerprint(this.FullViewingKey.Key);

			/// <inheritdoc/>
			public FullViewingKeyTag ParentFullViewingKeyTag { get; }

			/// <inheritdoc/>
			public ChainCode ChainCode { get; }

			/// <inheritdoc/>
			public uint ChildIndex { get; }

			/// <inheritdoc/>
			public byte Depth { get; }

			/// <inheritdoc/>
			public ZcashNetwork Network { get; }

			/// <inheritdoc/>
			bool IKey.IsTestNet => this.Network != ZcashNetwork.MainNet;

			/// <summary>
			/// Gets the default address for this spending key.
			/// </summary>
			/// <remarks>
			/// Create additional diversified addresses using <see cref="DiversifiableFullViewingKey.TryCreateReceiver(ref BigInteger, out SaplingReceiver)"/>
			/// found on the <see cref="FullViewingKey"/> property's <see cref="ExtendedFullViewingKey.Key"/> property.
			/// </remarks>
			public SaplingAddress DefaultAddress => new(this.FullViewingKey.Key.CreateDefaultReceiver(), this.Network);

			/// <summary>
			/// Gets the Bech32 encoding of the spending key.
			/// </summary>
			/// <remarks>
			/// To instantiate a key from this encoding, use the <see cref="FromEncoded(ReadOnlySpan{char})"/> method.
			/// </remarks>
			public string Encoded
			{
				get
				{
					Span<byte> encodedBytes = stackalloc byte[169];
					Span<char> encodedChars = stackalloc char[512];
					int byteLength = this.Encode(encodedBytes);
					string hrp = this.Network switch
					{
						ZcashNetwork.MainNet => Bech32MainNetworkHRP,
						ZcashNetwork.TestNet => Bech32TestNetworkHRP,
						_ => throw new NotSupportedException(),
					};
					int charLength = Bech32.Original.Encode(hrp, encodedBytes[..byteLength], encodedChars);
					return new string(encodedChars[..charLength]);
				}
			}

			/// <summary>
			/// Gets the expanded spending key (one that has ask, nsk, and ovk derived from the raw 32-byte spending key).
			/// </summary>
			internal ExpandedSpendingKey ExpandedSpendingKey { get; }

			/// <summary>
			/// Gets the diversifier key.
			/// </summary>
			internal DiversifierKey Dk { get; }

			/// <summary>
			/// Initializes a new instance of the <see cref="ExtendedSpendingKey"/> class
			/// from the bech32 encoding of an extended spending key as specified in ZIP-32.
			/// </summary>
			/// <param name="encoding">The bech32-encoded key.</param>
			/// <returns>An initialized <see cref="ExtendedSpendingKey"/>.</returns>
			/// <remarks>
			/// This method can parse the output of the <see cref="Encoded"/> property.
			/// </remarks>
			public static ExtendedSpendingKey FromEncoded(ReadOnlySpan<char> encoding)
			{
				Span<char> hrp = stackalloc char[50];
				Span<byte> data = stackalloc byte[169];
				(int tagLength, int dataLength) = Bech32.Original.Decode(encoding, hrp, data);
				hrp = hrp[..tagLength];
				ZcashNetwork network = hrp switch
				{
					Bech32MainNetworkHRP => ZcashNetwork.MainNet,
					Bech32TestNetworkHRP => ZcashNetwork.TestNet,
					_ => throw new InvalidKeyException($"Unexpected bech32 tag: {hrp}"),
				};
				return Decode(data[..dataLength], network);
			}

			/// <inheritdoc cref="Cryptocurrencies.IExtendedKey.Derive(uint)"/>
			public ExtendedSpendingKey Derive(uint childIndex)
			{
				Span<byte> selfAsBytes = stackalloc byte[169];
				Span<byte> childAsBytes = stackalloc byte[169];
				this.Encode(selfAsBytes);
				if (NativeMethods.DeriveSaplingChild(selfAsBytes, childIndex, childAsBytes) != 0)
				{
					throw new InvalidKeyException();
				}

				return Decode(childAsBytes, this.Network);
			}

			/// <inheritdoc/>
			Cryptocurrencies.IExtendedKey Cryptocurrencies.IExtendedKey.Derive(uint childIndex) => this.Derive(childIndex);

			/// <inheritdoc/>
			public bool Equals(ExtendedSpendingKey? other)
			{
				return other is not null
					&& this.ChainCode.Value.SequenceEqual(other.ChainCode.Value)
					&& this.ExpandedSpendingKey.Equals(other.ExpandedSpendingKey)
					&& this.Dk.Value.SequenceEqual(other.Dk.Value)
					&& this.ParentFullViewingKeyTag.Value.SequenceEqual(other.ParentFullViewingKeyTag.Value)
					&& this.Depth == other.Depth
					&& this.ChildIndex == other.ChildIndex
					&& this.Network == other.Network;
			}

			private static ExtendedSpendingKey Decode(ReadOnlySpan<byte> encoded, ZcashNetwork network)
			{
				byte depth = encoded[0];
				FullViewingKeyTag parentFullViewingKeyTag = new(encoded[1..5]);
				uint childIndex = BitUtilities.ReadUInt32LE(encoded[5..9]);
				ChainCode chainCode = new(encoded[9..41]);
				ExpandedSpendingKey expsk = ExpandedSpendingKey.FromBytes(encoded[41..137]);
				DiversifierKey dk = new(encoded[137..169]);
				return new(expsk, dk, chainCode, parentFullViewingKeyTag, depth, childIndex, network);
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
				length += BitUtilities.WriteLE(this.ChildIndex, result[length..]);
				length += this.ChainCode.Value.CopyToRetLength(result[length..]);
				length += this.ExpandedSpendingKey.ToBytes(result[length..]);
				length += this.Dk.Value.CopyToRetLength(result[length..]);
				Assumes.True(length == 169);
				return length;
			}
		}
	}
}
