// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using NBitcoin.Secp256k1;
using Nerdbank.Bitcoin;
using static Nerdbank.Bitcoin.Bip32HDWallet;

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
			private const int ExpectedUnifiedDataLength = 74;

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
				: base(key, chainCode, parentFullViewingKeyTag.AsParentFingerprint, depth, childIndex, network.IsTestNet())
			{
				this.Network = network;
				this.Key = new Zcash.Transparent.PrivateKey(key, network);
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
				this.Key = new Zcash.Transparent.PrivateKey(this.CryptographicKey, network);
			}

			/// <summary>
			/// Gets the private key.
			/// </summary>
			public new Zcash.Transparent.PrivateKey Key { get; }

			/// <inheritdoc/>
			public ZcashNetwork Network { get; }

			/// <inheritdoc cref="IIncomingViewingKey.DefaultAddress"/>
			public TransparentAddress DefaultAddress => this.FullViewingKey.IncomingViewingKey.DefaultAddress;

			/// <inheritdoc/>
			public ref readonly FullViewingKeyFingerprint Fingerprint => throw new NotSupportedException();

			/// <inheritdoc/>
			public ref readonly FullViewingKeyTag ParentFullViewingKeyTag => ref FullViewingKeyTag.From(this.ParentFingerprint);

			/// <inheritdoc/>
			public new ref readonly ChainCode ChainCode => ref base.ChainCode;

			/// <inheritdoc cref="ISpendingKey.FullViewingKey"/>
			public ExtendedViewingKey FullViewingKey => new(this.PublicKey, this.Network);

			/// <inheritdoc/>
			IFullViewingKey ISpendingKey.FullViewingKey => this.FullViewingKey;

			/// <inheritdoc/>
			byte IUnifiedEncodingElement.UnifiedTypeCode => UnifiedTypeCodes.TransparentP2PKH;

			/// <inheritdoc/>
			int IUnifiedEncodingElement.UnifiedDataLength => ExpectedUnifiedDataLength;

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
					&& this.ChainCode.Equals(other.ChainCode)
					&& this.ParentFullViewingKeyTag.Equals(other.ParentFullViewingKeyTag)
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
				Span<byte> scratch = stackalloc byte[78];

				// This is standard BIP32 serialization, except without the Version header.
				int written = this.WriteBytes(destination);
				destination[4..].CopyTo(destination);
				written -= 4;

				return written;
			}

			/// <inheritdoc cref="Zcash.Orchard.SpendingKey.DecodeUnifiedKeyContribution(ReadOnlySpan{byte}, ZcashNetwork)"/>
			internal static IUnifiedEncodingElement DecodeUnifiedKeyContribution(ReadOnlySpan<byte> keyContribution, ZcashNetwork network)
			{
				Requires.Argument(keyContribution.Length == ExpectedUnifiedDataLength, nameof(keyContribution), $"Length expected to be {ExpectedUnifiedDataLength}.");

				const int PublicKeyLength = 33;

				byte depth = keyContribution[0];
				ref readonly ParentFingerprint parentFingerprint = ref ParentFingerprint.From(keyContribution[1..5]);
				uint childIndex = BitUtilities.ReadUInt32BE(keyContribution[5..9]);
				ref readonly ChainCode chainCode = ref ChainCode.From(keyContribution.Slice(9, ChainCode.Length));
				ReadOnlySpan<byte> keyMaterial = keyContribution[^PublicKeyLength..];

				return new ExtendedSpendingKey(
					ECPrivKey.Create(keyMaterial),
					new ChainCode(chainCode),
					FullViewingKeyTag.From(parentFingerprint),
					depth,
					childIndex,
					network);
			}
		}
	}
}
