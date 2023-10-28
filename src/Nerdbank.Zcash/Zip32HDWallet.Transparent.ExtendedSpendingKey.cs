// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using NBitcoin.Secp256k1;
using Nerdbank.Bitcoin;

namespace Nerdbank.Zcash;

public partial class Zip32HDWallet
{
	public partial class Transparent
	{
		/// <summary>
		/// The extended spending key.
		/// </summary>
		[DebuggerDisplay($"{{{nameof(DebuggerDisplay)},nq}}")]
		public class ExtendedSpendingKey : Bip32HDWallet.ExtendedPrivateKey, IExtendedKey, ISpendingKey, IUnifiedEncodingElement, IEquatable<ExtendedSpendingKey>, IKeyWithTextEncoding
		{
			/// <summary>
			/// Initializes a new instance of the <see cref="ExtendedSpendingKey"/> class
			/// that represents a derived key.
			/// </summary>
			/// <param name="key">The cryptographic key.</param>
			/// <param name="chainCode">The chain code.</param>
			/// <param name="parentFullViewingKeyTag">The first four bytes from the parent key's <see cref="Fingerprint"/>.</param>
			/// <param name="depth">The number of derivations between this key and the master key.</param>
			/// <param name="childIndex">The index of this key among its peers.</param>
			/// <param name="network">The Zcash network this key should be used on.</param>
			internal ExtendedSpendingKey(ECPrivKey key, in ChainCode chainCode, in FullViewingKeyTag parentFullViewingKeyTag, byte depth, uint childIndex, ZcashNetwork network)
				: base(key, chainCode.Value, parentFullViewingKeyTag.Value, depth, childIndex, network.IsTestNet())
			{
				this.Network = network;
			}

			/// <summary>
			/// Initializes a new instance of the <see cref="ExtendedSpendingKey"/> class.
			/// </summary>
			/// <param name="copyFrom">The BIP32 key to copy from.</param>
			/// <param name="network">The Zcash network this key should be used on.</param>
			protected internal ExtendedSpendingKey(Bip32HDWallet.ExtendedPrivateKey copyFrom, ZcashNetwork network)
				: base(copyFrom)
			{
				this.Network = network;
			}

			/// <inheritdoc/>
			public ZcashNetwork Network { get; }

			/// <inheritdoc cref="IIncomingViewingKey.DefaultAddress"/>
			public TransparentAddress DefaultAddress => this.FullViewingKey.IncomingViewingKey.DefaultAddress;

			/// <inheritdoc/>
			public FullViewingKeyFingerprint Fingerprint => throw new NotSupportedException();

			/// <inheritdoc/>
			public FullViewingKeyTag ParentFullViewingKeyTag => new(this.ParentFingerprint);

			/// <inheritdoc/>
			public new ChainCode ChainCode => new(base.ChainCode);

			/// <inheritdoc cref="ISpendingKey.FullViewingKey"/>
			public ExtendedViewingKey FullViewingKey => new(this.PublicKey, this.Network);

			/// <inheritdoc/>
			IFullViewingKey ISpendingKey.FullViewingKey => this.FullViewingKey;

			/// <inheritdoc/>
			byte IUnifiedEncodingElement.UnifiedTypeCode => UnifiedTypeCodes.TransparentP2PKH;

			/// <inheritdoc/>
			int IUnifiedEncodingElement.UnifiedDataLength => 64;

			private new string DebuggerDisplay => $"{this.DefaultAddress} ({this.DerivationPath})";

			/// <inheritdoc cref="IKeyWithTextEncoding.TryDecode(string, out DecodeError?, out string?, out IKeyWithTextEncoding?)"/>
			public static bool TryDecode(ReadOnlySpan<char> encoding, [NotNullWhen(false)] out DecodeError? decodeError, [NotNullWhen(false)] out string? errorMessage, [NotNullWhen(true)] out ExtendedSpendingKey? key)
			{
				if (!TryDecode(encoding, out decodeError, out errorMessage, out Bip32HDWallet.ExtendedKeyBase? bip32Key))
				{
					key = null;
					return false;
				}

				if (bip32Key is Bip32HDWallet.ExtendedPrivateKey privateKey)
				{
					key = new(privateKey, bip32Key.IsTestNet ? ZcashNetwork.TestNet : ZcashNetwork.MainNet);
					return true;
				}

				decodeError = DecodeError.UnrecognizedHRP;
				errorMessage = "The key is not a private key.";
				key = null;
				return false;
			}

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

			/// <inheritdoc/>
			public override ExtendedSpendingKey Derive(uint childIndex) => new(base.Derive(childIndex), this.Network);

			/// <inheritdoc/>
			IExtendedKey IExtendedKey.Derive(uint childIndex) => this.Derive(childIndex);

			/// <inheritdoc/>
			public bool Equals(ExtendedSpendingKey? other)
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
			public override bool Equals(object? obj) => obj is ExtendedSpendingKey other && this.Equals(other);

			/// <inheritdoc/>
			public override int GetHashCode()
			{
				HashCode result = default;
				result.Add(this.Network);
				result.AddBytes(this.Identifier);
				return result.ToHashCode();
			}

			/// <inheritdoc/>
			int IUnifiedEncodingElement.WriteUnifiedData(Span<byte> destination)
			{
				int written = 0;
				this.CryptographicKey.WriteToSpan(destination);
				written += 32;

				written += this.ChainCode.Value.CopyToRetLength(destination);
				Assumes.True(written == 64);
				return written;
			}

			/// <inheritdoc cref="Zcash.Orchard.SpendingKey.DecodeUnifiedViewingKeyContribution(ReadOnlySpan{byte}, ZcashNetwork)"/>
			internal static IUnifiedEncodingElement DecodeUnifiedViewingKeyContribution(ReadOnlySpan<byte> keyContribution, ZcashNetwork network)
			{
				Requires.Argument(keyContribution.Length == 64, nameof(keyContribution), "Length expected to be 64.");
				ReadOnlySpan<byte> publicKeyData = keyContribution[..32];
				ReadOnlySpan<byte> chainCode = keyContribution[32..64];

				// We have to assume or bluff on some of these values, since they aren't preserved by the encoding.
				// The values don't actually matter as they don't impact the generated cryptographic key.
				FullViewingKeyTag parentFingerprintTag = default; // we don't know it, but that's OK.
				byte depth = 3; // A full viewing key always has a depth of 3.
				uint childIndex = 0; // We don't know it, but that's OK.

				return new ExtendedSpendingKey(
					ECPrivKey.Create(publicKeyData),
					new ChainCode(chainCode),
					parentFingerprintTag,
					depth,
					childIndex,
					network);
			}
		}
	}
}
