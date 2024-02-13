// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Zcash.Sapling;
using static Nerdbank.Bitcoin.Bip32HDWallet;

namespace Nerdbank.Zcash;

public partial class Zip32HDWallet
{
	public partial class Sapling
	{
		/// <summary>
		/// The full viewing key, extended so it can be used to derive child keys.
		/// </summary>
		[DebuggerDisplay($"{{{nameof(DebuggerDisplay)},nq}}")]
		public class ExtendedFullViewingKey : IExtendedKey, IFullViewingKey, IEquatable<ExtendedFullViewingKey>, IKeyWithTextEncoding
		{
			private const string Bech32MainNetworkHRP = "zxviews";
			private const string Bech32TestNetworkHRP = "zxviewtestsapling";

			private readonly FullViewingKeyFingerprint fingerprint;
			private readonly FullViewingKeyTag parentFullViewingKeyTag;
			private readonly ChainCode chainCode;
			private string? textEncoding;

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
				this.fingerprint = GetFingerprint(key);
				this.parentFullViewingKeyTag = parentFullViewingKeyTag;
				this.chainCode = chainCode;
				this.ChildIndex = childIndex;
				this.Depth = depth;
			}

			/// <inheritdoc/>
			public ref readonly FullViewingKeyFingerprint Fingerprint => ref this.fingerprint;

			/// <inheritdoc/>
			public ref readonly FullViewingKeyTag ParentFullViewingKeyTag => ref this.parentFullViewingKeyTag;

			/// <inheritdoc/>
			public ref readonly ChainCode ChainCode => ref this.chainCode;

			/// <inheritdoc/>
			public uint ChildIndex { get; }

			/// <inheritdoc/>
			public byte Depth { get; }

			/// <inheritdoc/>
			public Bip32KeyPath? DerivationPath { get; init; }

			/// <inheritdoc/>
			public ZcashNetwork Network => this.FullViewingKey.Network;

			/// <summary>
			/// Gets the full viewing key.
			/// </summary>
			public DiversifiableFullViewingKey FullViewingKey { get; }

			/// <summary>
			/// Gets the incoming viewing key.
			/// </summary>
			public DiversifiableIncomingViewingKey IncomingViewingKey => this.FullViewingKey.IncomingViewingKey;

			/// <inheritdoc/>
			IIncomingViewingKey IFullViewingKey.IncomingViewingKey => this.IncomingViewingKey;

			/// <summary>
			/// Gets the Bech32 encoding of the full viewing key.
			/// </summary>
			public string TextEncoding
			{
				get
				{
					if (this.textEncoding is null)
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
						this.textEncoding = new string(encodedChars[..charLength]);
					}

					return this.textEncoding;
				}
			}

			/// <summary>
			/// Gets the default address for this spending key.
			/// </summary>
			/// <remarks>
			/// Create additional diversified addresses using <see cref="DiversifiableIncomingViewingKey.TryCreateReceiver"/>
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
			/// <inheritdoc cref="IKeyWithTextEncoding.TryDecode(string, out DecodeError?, out string?, out IKeyWithTextEncoding?)"/>
			public static bool TryDecode(ReadOnlySpan<char> encoding, [NotNullWhen(false)] out DecodeError? decodeError, [NotNullWhen(false)] out string? errorMessage, [NotNullWhen(true)] out ExtendedFullViewingKey? key)
			{
				Span<char> hrp = stackalloc char[50];
				Span<byte> data = stackalloc byte[169];
				if (!Bech32.Original.TryDecode(encoding, hrp, data, out decodeError, out errorMessage, out (int TagLength, int DataLength) length))
				{
					key = null;
					return false;
				}

				hrp = hrp[..length.TagLength];
				ZcashNetwork? network = hrp switch
				{
					Bech32MainNetworkHRP => ZcashNetwork.MainNet,
					Bech32TestNetworkHRP => ZcashNetwork.TestNet,
					_ => null,
				};
				if (network is null)
				{
					decodeError = DecodeError.UnrecognizedHRP;
					errorMessage = $"Unexpected bech32 tag: {hrp}";
					key = null;
					return false;
				}

				key = Decode(data[..length.DataLength], network.Value);
				return true;
			}

			/// <inheritdoc cref="IKeyWithTextEncoding.TryDecode(string, out DecodeError?, out string?, out IKeyWithTextEncoding?)"/>
			static bool IKeyWithTextEncoding.TryDecode(string encoding, [NotNullWhen(false)] out DecodeError? decodeError, [NotNullWhen(false)] out string? errorMessage, [NotNullWhen(true)] out IKeyWithTextEncoding? key)
			{
				if (TryDecode(encoding, out decodeError, out errorMessage, out ExtendedFullViewingKey? fvk))
				{
					key = fvk;
					return true;
				}

				key = null;
				return false;
			}

			/// <inheritdoc cref="DiversifiableFullViewingKey.DeriveInternal"/>
			public ExtendedFullViewingKey DeriveInternal()
			{
				return new ExtendedFullViewingKey(
					this.FullViewingKey.DeriveInternal(),
					this.ChainCode,
					this.ParentFullViewingKeyTag,
					this.Depth,
					this.ChildIndex);
			}

			/// <inheritdoc/>
			public bool Equals(ExtendedFullViewingKey? other)
			{
				return other is not null
					&& this.FullViewingKey.Equals(other.FullViewingKey)
					&& this.ChainCode.Equals(other.ChainCode)
					&& this.ParentFullViewingKeyTag.Equals(other.ParentFullViewingKeyTag)
					&& this.Depth == other.Depth
					&& this.ChildIndex == other.ChildIndex
					&& this.Network == other.Network;
			}

			/// <inheritdoc/>
			[Obsolete("Derive from spending keys instead.")]
			Cryptocurrencies.IExtendedKey Cryptocurrencies.IExtendedKey.Derive(uint childIndex) => this.Derive(childIndex);

			/// <inheritdoc/>
			[Obsolete("Derive from spending keys instead.")]
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

			/// <inheritdoc cref="Cryptocurrencies.IExtendedKey.Derive(uint)"/>
			[Obsolete("Derive from spending keys instead.")]
			private ExtendedFullViewingKey Derive(uint childIndex)
			{
				throw new NotSupportedException("Derive using spending keys, because per ZIP-32 the derivation path is hardened so deriving full viewing keys is pointless.");
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
				length += this.ParentFullViewingKeyTag[..].CopyToRetLength(result[length..]);
				length += BitUtilities.WriteLE(this.ChildIndex, result[length..]);
				length += this.ChainCode[..].CopyToRetLength(result[length..]);
				length += this.FullViewingKey.Encode(result[length..]);
				Assumes.True(length == 169);
				return length;
			}
		}
	}
}
