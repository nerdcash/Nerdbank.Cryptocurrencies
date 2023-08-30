// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using NBitcoin.Secp256k1;
using static Nerdbank.Cryptocurrencies.Bip32HDWallet;

namespace Nerdbank.Zcash;

public partial class Zip32HDWallet
{
	public partial class Transparent
	{
		/// <summary>
		/// The full viewing key, extended so it can be used to derive child keys.
		/// </summary>
		[DebuggerDisplay($"{{{nameof(DebuggerDisplay)},nq}}")]
		public class ExtendedViewingKey : ExtendedPublicKey, IExtendedKey, IFullOrIncomingViewingKey, IUnifiedEncodingElement, IEquatable<ExtendedViewingKey>
		{
			/// <summary>
			/// Initializes a new instance of the <see cref="ExtendedViewingKey"/> class.
			/// </summary>
			/// <param name="copyFrom">The BIP32 key to copy from.</param>
			/// <param name="network">The Zcash network this key should be used on.</param>
			public ExtendedViewingKey(ExtendedPublicKey copyFrom, ZcashNetwork network)
				: base(copyFrom)
			{
				Requires.NotNull(copyFrom);
				Requires.Argument(copyFrom.IsTestNet == network.IsTestNet(), nameof(network), "Does not agree with IsTestNet on base type.");
				this.Network = network;
			}

			/// <summary>
			/// Initializes a new instance of the <see cref="ExtendedViewingKey"/> class
			/// that represents a derived key.
			/// </summary>
			/// <param name="key">The cryptographic key.</param>
			/// <param name="chainCode">The chain code.</param>
			/// <param name="parentFullViewingKeyTag">The first four bytes from the parent key's <see cref="Fingerprint"/>.</param>
			/// <param name="depth">The number of derivations between this key and the master key.</param>
			/// <param name="childIndex">The index of this key among its peers.</param>
			/// <param name="network">The Zcash network this key should be used on.</param>
			internal ExtendedViewingKey(ECPubKey key, in ChainCode chainCode, in FullViewingKeyTag parentFullViewingKeyTag, byte depth, uint childIndex, ZcashNetwork network)
				: base(key, chainCode.Value, parentFullViewingKeyTag.Value, depth, childIndex, network.IsTestNet())
			{
				this.Network = network;
			}

			/// <inheritdoc/>
			public ZcashNetwork Network { get; }

			/// <inheritdoc cref="IIncomingViewingKey.DefaultAddress"/>
			public TransparentAddress DefaultAddress => throw new NotImplementedException();

			/// <inheritdoc/>
			public FullViewingKeyFingerprint Fingerprint => throw new NotSupportedException();

			/// <inheritdoc/>
			public FullViewingKeyTag ParentFullViewingKeyTag => new(this.ParentFingerprint);

			/// <inheritdoc/>
			public new ChainCode ChainCode => new(base.ChainCode);

			/// <summary>
			/// Gets the incoming viewing key, which is the receiving address chain derivation of the full viewing key.
			/// </summary>
			/// <remarks>
			/// As <see href="https://zips.z.cash/zip-0316#encoding-of-unified-full-incoming-viewing-keys">specified by ZIP-316</see>.
			/// </remarks>
			public ExtendedViewingKey IncomingViewingKey
			{
				get
				{
					return this.Depth switch
					{
						3 => this.Derive((uint)Bip44MultiAccountHD.Change.ReceivingAddressChain),
						4 when this.ChildIndex is 0 => this,
						_ => throw new InvalidOperationException("This is not a full viewing key or incoming viewing key."),
					};
				}
			}

			/// <inheritdoc/>
			IIncomingViewingKey IFullViewingKey.IncomingViewingKey => this.IncomingViewingKey;

			/// <inheritdoc/>
			public bool IsFullViewingKey => this.Depth == 3;

			/// <inheritdoc/>
			byte IUnifiedEncodingElement.UnifiedTypeCode => UnifiedTypeCodes.TransparentP2PKH;

			/// <inheritdoc/>
			int IUnifiedEncodingElement.UnifiedDataLength => 65;

			private new string DebuggerDisplay => $"{this.DefaultAddress} ({this.DerivationPath})";

			/// <inheritdoc/>
			public override ExtendedViewingKey Derive(uint childIndex) => new(base.Derive(childIndex), this.Network);

			/// <inheritdoc/>
			IExtendedKey IExtendedKey.Derive(uint childIndex) => this.Derive(childIndex);

			/// <inheritdoc/>
			public bool Equals(ExtendedViewingKey? other)
			{
				return other is not null
					&& this.Identifier.SequenceEqual(other.Identifier)
					&& this.ChainCode.Value.SequenceEqual(other.ChainCode.Value)
					&& this.ParentFullViewingKeyTag.Value.SequenceEqual(other.ParentFullViewingKeyTag.Value)
					&& this.Depth == other.Depth
					&& this.ChildIndex == other.ChildIndex
					&& this.Network == other.Network;
			}

			/// <inheritdoc/>
			int IUnifiedEncodingElement.WriteUnifiedData(Span<byte> destination)
			{
				int written = 0;
				written += this.ChainCode.Value.CopyToRetLength(destination[written..]);
				this.CryptographicKey.WriteToSpan(compressed: true, destination[written..], out int keyLength);
				written += keyLength;
				Assumes.True(written == 65);
				return written;
			}

			/// <inheritdoc cref="Zcash.Orchard.SpendingKey.DecodeUnifiedViewingKeyContribution(ReadOnlySpan{byte}, ZcashNetwork)"/>
			internal static IUnifiedEncodingElement DecodeUnifiedViewingKeyContribution(ReadOnlySpan<byte> keyContribution, ZcashNetwork network)
			{
				ReadOnlySpan<byte> chainCode = keyContribution[..32];
				ReadOnlySpan<byte> publicKeyData = keyContribution[33..];

				// We have to assume or bluff on some of these values, since they aren't preserved by the encoding.
				// The values don't actually matter as they don't impact the generated cryptographic key.
				FullViewingKeyTag parentFingerprintTag = default; // we don't know it, but that's OK.
				byte depth = 3; // A full viewing key always has a depth of 3.
				uint childIndex = 0; // We don't know it, but that's OK.

				return new ExtendedViewingKey(
					ECPubKey.Create(publicKeyData),
					new ChainCode(chainCode),
					parentFingerprintTag,
					depth,
					childIndex,
					network);
			}
		}
	}
}
