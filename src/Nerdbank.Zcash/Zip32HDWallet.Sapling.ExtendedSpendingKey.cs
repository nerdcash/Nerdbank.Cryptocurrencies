﻿// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Zcash.Orchard;
using Nerdbank.Zcash.Sapling;
using static Nerdbank.Bitcoin.Bip32HDWallet;

namespace Nerdbank.Zcash;

public partial class Zip32HDWallet
{
	public partial class Sapling
	{
		/// <summary>
		/// The extended spending key.
		/// </summary>
		[DebuggerDisplay($"{{{nameof(DebuggerDisplay)},nq}}")]
		public class ExtendedSpendingKey : IExtendedKey, ISpendingKey, IUnifiedEncodingElement, IEquatable<ExtendedSpendingKey>, IKeyWithTextEncoding
		{
			private const string Bech32MainNetworkHRP = "secret-extended-key-main";
			private const string Bech32TestNetworkHRP = "secret-extended-key-test";
			private readonly FullViewingKeyTag parentFullViewingKeyTag;
			private readonly FullViewingKeyFingerprint fingerprint;
			private readonly ChainCode chainCode;

			/// <summary>
			/// Backing field for the <see cref="ExtendedFullViewingKey"/> property.
			/// </summary>
			private ExtendedFullViewingKey? extendedFullViewingKey;

			private string? textEncoding;

			/// <summary>
			/// Initializes a new instance of the <see cref="ExtendedSpendingKey"/> class.
			/// </summary>
			/// <param name="key">The spending key's ask, nsk and ovk components.</param>
			/// <param name="chainCode">The chain code.</param>
			/// <param name="parentFullViewingKeyTag">The tag from the full viewing key. Use the default value if not derived.</param>
			/// <param name="depth">The derivation depth of this key. Use 0 if there is no parent.</param>
			/// <param name="childIndex">The derivation number used to derive this key from its parent. Use 0 if there is no parent.</param>
			internal ExtendedSpendingKey(in ExpandedSpendingKey key, in ChainCode chainCode, in FullViewingKeyTag parentFullViewingKeyTag, byte depth, uint childIndex)
			{
				this.ExpandedSpendingKey = key;
				this.fingerprint = GetFingerprint(key.FullViewingKey);
				this.chainCode = chainCode;
				this.parentFullViewingKeyTag = parentFullViewingKeyTag;
				this.Depth = depth;
				this.ChildIndex = childIndex;

				this.FullViewingKey = new(
					Zcash.Sapling.FullViewingKey.Create(key.Ask, key.Nsk, key.Ovk, key.Network),
					key.Dk);
			}

			/// <inheritdoc/>
			public Bip32KeyPath? DerivationPath { get; init; }

			/// <summary>
			/// Gets the extended full viewing key.
			/// </summary>
			public ExtendedFullViewingKey ExtendedFullViewingKey => this.extendedFullViewingKey ??= new ExtendedFullViewingKey(this.FullViewingKey, this.ChainCode, this.ParentFullViewingKeyTag, this.Depth, this.ChildIndex) { DerivationPath = this.DerivationPath };

			/// <summary>
			/// Gets the full viewing key.
			/// </summary>
			public DiversifiableFullViewingKey FullViewingKey { get; }

			/// <inheritdoc/>
			IFullViewingKey ISpendingKey.FullViewingKey => this.FullViewingKey;

			/// <summary>
			/// Gets the incoming viewing key.
			/// </summary>
			public DiversifiableIncomingViewingKey IncomingViewingKey => this.FullViewingKey.IncomingViewingKey;

			/// <inheritdoc/>
			IIncomingViewingKey IFullViewingKey.IncomingViewingKey => this.IncomingViewingKey;

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
			public ZcashNetwork Network => this.ExpandedSpendingKey.Network;

			/// <summary>
			/// Gets the default address for this spending key.
			/// </summary>
			/// <remarks>
			/// Create additional diversified addresses using <see cref="DiversifiableIncomingViewingKey.TryCreateReceiver"/>
			/// found on the <see cref="FullViewingKey"/>.
			/// </remarks>
			public SaplingAddress DefaultAddress => this.IncomingViewingKey.DefaultAddress;

			/// <summary>
			/// Gets the Bech32 encoding of the spending key.
			/// </summary>
			/// <inheritdoc/>
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

			/// <inheritdoc/>
			byte IUnifiedEncodingElement.UnifiedTypeCode => UnifiedTypeCodes.Sapling;

			/// <inheritdoc/>
			int IUnifiedEncodingElement.UnifiedDataLength => 169;

			/// <summary>
			/// Gets the expanded spending key (one that has ask, nsk, and ovk derived from the raw 32-byte spending key).
			/// </summary>
			public ExpandedSpendingKey ExpandedSpendingKey { get; }

			/// <summary>
			/// Gets the diversifier key.
			/// </summary>
			internal ref readonly DiversifierKey Dk => ref this.ExpandedSpendingKey.Dk;

			private string DebuggerDisplay => $"{this.DefaultAddress} ({this.DerivationPath})";

			/// <inheritdoc cref="IKeyWithTextEncoding.TryDecode(string, out DecodeError?, out string?, out IKeyWithTextEncoding?)"/>
			static bool IKeyWithTextEncoding.TryDecode(string encoding, [NotNullWhen(false)] out DecodeError? decodeError, [NotNullWhen(false)] out string? errorMessage, [NotNullWhen(true)] out IKeyWithTextEncoding? key)
			{
				if (TryDecode(encoding, out decodeError, out errorMessage, out ExtendedSpendingKey? sk))
				{
					key = sk;
					return true;
				}

				key = null;
				return false;
			}

			/// <summary>
			/// Initializes a new instance of the <see cref="ExtendedSpendingKey"/> class
			/// from the bech32 encoding of an extended spending key as specified in ZIP-32.
			/// </summary>
			/// <inheritdoc cref="IKeyWithTextEncoding.TryDecode(string, out DecodeError?, out string?, out IKeyWithTextEncoding?)"/>
			public static bool TryDecode(ReadOnlySpan<char> encoding, [NotNullWhen(false)] out DecodeError? decodeError, [NotNullWhen(false)] out string? errorMessage, [NotNullWhen(true)] out ExtendedSpendingKey? key)
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
			IExtendedKey IExtendedKey.Derive(uint childIndex) => this.Derive(childIndex);

			/// <summary>
			/// Derives the internal address spending key from this.
			/// </summary>
			/// <returns>The extended spending key.</returns>
			/// <remarks>
			/// This method assumes that <em>this</em> spending key is the public facing one.
			/// The caller should take care to not call this method on what is already the internal key.
			/// </remarks>
			public ExtendedSpendingKey DeriveInternal()
			{
				Span<byte> encoded = stackalloc byte[169];
				int length = this.Encode(encoded);
				Assumes.True(length == encoded.Length);
				Span<byte> encodedInternal = stackalloc byte[169];
				int result = NativeMethods.DeriveSaplingInternalSpendingKey(encoded, encodedInternal);
				if (result != 0)
				{
					throw new InvalidKeyException($"Derivation failure: {result}");
				}

				return Decode(encodedInternal, this.Network);
			}

			/// <inheritdoc/>
			public bool Equals(ExtendedSpendingKey? other)
			{
				return other is not null
					&& this.ChainCode.Equals(other.ChainCode)
					&& this.ExpandedSpendingKey.Equals(other.ExpandedSpendingKey)
					&& this.Dk.Equals(other.Dk)
					&& this.ParentFullViewingKeyTag.Equals(other.ParentFullViewingKeyTag)
					&& this.Depth == other.Depth
					&& this.ChildIndex == other.ChildIndex
					&& this.Network == other.Network;
			}

			/// <inheritdoc/>
			int IUnifiedEncodingElement.WriteUnifiedData(Span<byte> destination) => this.Encode(destination);

			/// <inheritdoc cref="Zcash.Orchard.SpendingKey.DecodeUnifiedKeyContribution(ReadOnlySpan{byte}, ZcashNetwork)"/>
			internal static IUnifiedEncodingElement DecodeUnifiedViewingKeyContribution(ReadOnlySpan<byte> keyContribution, ZcashNetwork network) => Decode(keyContribution, network);

			private static ExtendedSpendingKey Decode(ReadOnlySpan<byte> encoded, ZcashNetwork network)
			{
				byte depth = encoded[0];
				FullViewingKeyTag parentFullViewingKeyTag = new(encoded[1..5]);
				uint childIndex = BitUtilities.ReadUInt32LE(encoded[5..9]);
				ChainCode chainCode = new(encoded[9..41]);
				DiversifierKey dk = new(encoded[137..169]);
				ExpandedSpendingKey expsk = ExpandedSpendingKey.FromBytes(encoded[41..137], dk, network);
				return new(expsk, chainCode, parentFullViewingKeyTag, depth, childIndex);
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
				length += this.ParentFullViewingKeyTag[..].CopyToRetLength(result[length..]);
				length += BitUtilities.WriteLE(this.ChildIndex, result[length..]);
				length += this.ChainCode[..].CopyToRetLength(result[length..]);
				length += this.ExpandedSpendingKey.ToBytes(result[length..]);
				length += this.Dk[..].CopyToRetLength(result[length..]);
				Assumes.True(length == 169);
				return length;
			}
		}
	}
}
