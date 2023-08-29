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
		[DebuggerDisplay($"{{{nameof(DebuggerDisplay)},nq}}")]
		public class ExtendedFullViewingKey : IExtendedKey, IFullViewingKey, IEquatable<ExtendedFullViewingKey>
		{
			private const string Bech32MainNetworkHRP = "zxviews";
			private const string Bech32TestNetworkHRP = "zxviewtestsapling";

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
				this.FullViewingKey = key;
				this.ParentFullViewingKeyTag = parentFullViewingKeyTag;
				this.ChainCode = chainCode;
				this.ChildIndex = childIndex;
				this.Depth = depth;
			}

			/// <inheritdoc/>
			public FullViewingKeyFingerprint Fingerprint => GetFingerprint(this.FullViewingKey);

			/// <inheritdoc/>
			public FullViewingKeyTag ParentFullViewingKeyTag { get; }

			/// <inheritdoc/>
			public ChainCode ChainCode { get; }

			/// <inheritdoc/>
			public uint ChildIndex { get; }

			/// <inheritdoc/>
			public byte Depth { get; }

			/// <inheritdoc/>
			public Bip32HDWallet.KeyPath? DerivationPath { get; init; }

			/// <inheritdoc/>
			public ZcashNetwork Network => this.FullViewingKey.Network;

			/// <summary>
			/// Gets the full viewing key.
			/// </summary>
			public DiversifiableFullViewingKey FullViewingKey { get; }

			/// <summary>
			/// Gets the incoming viewing key.
			/// </summary>
			public IncomingViewingKey IncomingViewingKey => this.FullViewingKey.IncomingViewingKey;

			/// <inheritdoc/>
			IIncomingViewingKey IFullViewingKey.IncomingViewingKey => this.IncomingViewingKey;

			/// <summary>
			/// Gets the Bech32 encoding of the full viewing key.
			/// </summary>
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
			/// Gets the default address for this spending key.
			/// </summary>
			/// <remarks>
			/// Create additional diversified addresses using <see cref="IncomingViewingKey.TryCreateReceiver(ref BigInteger, out SaplingReceiver)"/>
			/// found on the <see cref="FullViewingKey"/>.
			/// </remarks>
			public SaplingAddress DefaultAddress => this.IncomingViewingKey.DefaultAddress;

			/// <summary>
			/// Gets the diversifier key.
			/// </summary>
			/// <value>A 32-byte buffer.</value>
			internal ref readonly DiversifierKey Dk => ref this.FullViewingKey.Dk;

			private string DebuggerDisplay => $"{this.DefaultAddress} ({this.DerivationPath})";

			/// <summary>
			/// Initializes a new instance of the <see cref="ExtendedFullViewingKey"/> class
			/// from the bech32 encoding of an extended full viewing key as specified in ZIP-32.
			/// </summary>
			/// <param name="encoding">The bech32-encoded key.</param>
			/// <returns>An initialized <see cref="ExtendedFullViewingKey"/>.</returns>
			/// <remarks>
			/// This method can parse the output of the <see cref="Encoded"/> property.
			/// </remarks>
			public static ExtendedFullViewingKey FromEncoded(ReadOnlySpan<char> encoding)
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
			public bool Equals(ExtendedFullViewingKey? other)
			{
				return other is not null
					&& this.FullViewingKey.Equals(other.FullViewingKey)
					&& this.ChainCode.Value.SequenceEqual(other.ChainCode.Value)
					&& this.ParentFullViewingKeyTag.Value.SequenceEqual(other.ParentFullViewingKeyTag.Value)
					&& this.Depth == other.Depth
					&& this.ChildIndex == other.ChildIndex
					&& this.Network == other.Network;
			}

			/// <inheritdoc/>
			Cryptocurrencies.IExtendedKey Cryptocurrencies.IExtendedKey.Derive(uint childIndex) => this.Derive(childIndex);

			/// <inheritdoc/>
			IExtendedKey IExtendedKey.Derive(uint childIndex) => this.Derive(childIndex);

			private static ExtendedFullViewingKey Decode(ReadOnlySpan<byte> encoded, ZcashNetwork network)
			{
				byte depth = encoded[0];
				FullViewingKeyTag parentFullViewingKeyTag = new(encoded[1..5]);
				uint childIndex = BitUtilities.ReadUInt32LE(encoded[5..9]);
				ChainCode chainCode = new(encoded[9..41]);
				FullViewingKey fvk = Zcash.Sapling.FullViewingKey.Decode(encoded[41..137], network);
				DiversifierKey dk = new(encoded[137..169]);
				DiversifiableFullViewingKey dfvk = new(fvk, dk);
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
				length += this.FullViewingKey.Encode(result[length..]);
				length += this.Dk.Value.CopyToRetLength(result[length..]);
				Assumes.True(length == 169);
				return length;
			}
		}
	}
}
