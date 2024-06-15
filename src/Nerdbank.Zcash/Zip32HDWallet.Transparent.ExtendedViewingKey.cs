// Copyright (c) Andrew Arnott. All rights reserved.
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
		/// The full viewing key, extended so it can be used to derive child keys.
		/// </summary>
		[DebuggerDisplay($"{{{nameof(DebuggerDisplay)},nq}}")]
		public class ExtendedViewingKey : ExtendedPublicKey, IExtendedKey, IFullOrIncomingViewingKey, IUnifiedEncodingElement, IEquatable<ExtendedViewingKey>, IUnifiedEncodingElementEqualityComparer, IKeyWithTextEncoding
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
				this.Key = new Zcash.Transparent.PublicKey(this.CryptographicKey, network);
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
				: base(key, chainCode, parentFullViewingKeyTag.AsParentFingerprint, depth, childIndex, network.IsTestNet())
			{
				this.Network = network;
				this.Key = new Zcash.Transparent.PublicKey(this.CryptographicKey, network);
			}

			/// <summary>
			/// Gets the public key.
			/// </summary>
			public new Zcash.Transparent.PublicKey Key { get; }

			/// <inheritdoc/>
			public ZcashNetwork Network { get; }

			/// <inheritdoc cref="IIncomingViewingKey.DefaultAddress"/>
			public TransparentAddress DefaultAddress
			{
				get
				{
					if (this.Depth == 5)
					{
						return new TransparentP2PKHAddress(new TransparentP2PKHReceiver(this), this.Network);
					}
					else
					{
						ExtendedViewingKey derived = this.GetReceivingKey(0);
						Assumes.True(derived.Depth == 5);
						return derived.DefaultAddress;
					}
				}
			}

			/// <inheritdoc/>
			ZcashAddress IIncomingViewingKey.DefaultAddress => this.DefaultAddress;

			/// <inheritdoc/>
			public ref readonly FullViewingKeyFingerprint Fingerprint => throw new NotSupportedException();

			/// <inheritdoc/>
			public ref readonly FullViewingKeyTag ParentFullViewingKeyTag => ref FullViewingKeyTag.From(this.ParentFingerprint);

			/// <inheritdoc/>
			public new ref readonly ChainCode ChainCode => ref base.ChainCode;

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

			/// <inheritdoc cref="IKeyWithTextEncoding.TryDecode(string, out DecodeError?, out string?, out IKeyWithTextEncoding?)"/>
			public static bool TryDecode(ReadOnlySpan<char> encoding, [NotNullWhen(false)] out DecodeError? decodeResult, [NotNullWhen(false)] out string? errorMessage, [NotNullWhen(true)] out ExtendedViewingKey? key)
			{
				if (!TryDecode(encoding, out decodeResult, out errorMessage, out ExtendedKeyBase? bip32Key))
				{
					key = null;
					return false;
				}

				if (bip32Key is ExtendedPublicKey publicKey)
				{
					key = new(publicKey, bip32Key.IsTestNet ? ZcashNetwork.TestNet : ZcashNetwork.MainNet);
					return true;
				}

				decodeResult = DecodeError.UnrecognizedHRP;
				errorMessage = "The key is not a private key.";
				key = null;
				return false;
			}

			/// <inheritdoc cref="IKeyWithTextEncoding.TryDecode(string, out DecodeError?, out string?, out IKeyWithTextEncoding?)"/>
			static bool IKeyWithTextEncoding.TryDecode(string encoding, [NotNullWhen(false)] out DecodeError? decodeError, [NotNullWhen(false)] out string? errorMessage, [NotNullWhen(true)] out IKeyWithTextEncoding? key)
			{
				if (TryDecode(encoding, out decodeError, out errorMessage, out ExtendedViewingKey? fvk))
				{
					key = fvk;
					return true;
				}

				key = null;
				return false;
			}

			/// <inheritdoc/>
			public override ExtendedViewingKey Derive(uint childIndex) => new(base.Derive(childIndex), this.Network);

			/// <inheritdoc/>
			IExtendedKey IExtendedKey.Derive(uint childIndex) => this.Derive(childIndex);

			/// <summary>
			/// Gets the viewing key on the <see cref="Bip44MultiAccountHD.Change.ReceivingAddressChain"/>
			/// for a given address index.
			/// </summary>
			/// <param name="addressIndex">The address index to generate the viewing key for.</param>
			/// <returns>The derived key.</returns>
			/// <exception cref="InvalidOperationException">Thrown if this instance does not conform to either a full or viewing key.</exception>
			public ExtendedViewingKey GetReceivingKey(uint addressIndex)
			{
				Bip32KeyPath derivationPath = this.Depth switch
				{
					3 => new Bip32KeyPath(addressIndex, new Bip32KeyPath((uint)Bip44MultiAccountHD.Change.ReceivingAddressChain)),
					4 when this.ChildIndex == (uint)Bip44MultiAccountHD.Change.ReceivingAddressChain => new Bip32KeyPath(addressIndex),
					_ => throw new InvalidOperationException("This is not the full or incoming viewing key."),
				};
				return this.Derive(derivationPath);
			}

			/// <summary>
			/// Checks whether a given transparent receiver was derived from this key
			/// (in other words: would ZEC sent to this receiver arrive in this account?).
			/// </summary>
			/// <param name="receiver">The receiver to test.</param>
			/// <param name="maxAddressIndex"><inheritdoc cref="TryGetAddressIndex(in TransparentP2PKHReceiver, uint, out Bip44MultiAccountHD.Change?, out uint?)" path="/param[@name='maxAddressIndex']"/></param>
			/// <returns><see langword="true"/> if this receiver would send ZEC to this account; otherwise <see langword="false"/>.</returns>
			/// <remarks>
			/// <para>This is a simpler front-end for the <see cref="TryGetAddressIndex(in TransparentP2PKHReceiver, uint, out Bip44MultiAccountHD.Change?, out uint?)"/> method,
			/// which runs a similar test but also provides the change and address index derivations that match the given receiver.</para>
			/// </remarks>
			public bool CheckReceiver(in TransparentP2PKHReceiver receiver, uint maxAddressIndex) => this.TryGetAddressIndex(receiver, maxAddressIndex, out _, out _);

			/// <summary>
			/// Checks whether a given transparent receiver was derived from this key
			/// (in other words: would ZEC sent to this receiver arrive in this account?).
			/// </summary>
			/// <param name="receiver">The receiver to test.</param>
			/// <param name="maxAddressIndex">
			/// The maximum address index to test.
			/// Since there are <see cref="uint.MaxValue"/> possibilities that could be checked,
			/// it is customary to only test addresses from 0 to some small number based on the addresses already known to be in use.
			/// </param>
			/// <param name="change">Receives the chain index (external or change) where a match was found.</param>
			/// <param name="addressIndex">Receives the address index where a match was found.</param>
			/// <returns><see langword="true"/> if this receiver would send ZEC to this account; otherwise <see langword="false"/>.</returns>
			public bool TryGetAddressIndex(in TransparentP2PKHReceiver receiver, uint maxAddressIndex, [NotNullWhen(true)] out Bip44MultiAccountHD.Change? change, [NotNullWhen(true)] out uint? addressIndex)
			{
				TransparentP2PKHReceiver receiverCopy = receiver;
				if (this.Depth == 4)
				{
					return TestChain(this, out change, out addressIndex);
				}

				return
					TestChain(this.Derive((uint)Bip44MultiAccountHD.Change.ReceivingAddressChain), out change, out addressIndex) ||
					TestChain(this.Derive((uint)Bip44MultiAccountHD.Change.ChangeAddressChain), out change, out addressIndex);

				bool TestChain(ExtendedViewingKey chainKey, [NotNullWhen(true)] out Bip44MultiAccountHD.Change? change, [NotNullWhen(true)] out uint? addressIndex)
				{
					for (uint i = 0; i <= maxAddressIndex; i++)
					{
						ExtendedViewingKey candidate = chainKey.Derive(i);
						if (receiverCopy.Equals(new TransparentP2PKHReceiver(chainKey.Derive(i))))
						{
							change = (Bip44MultiAccountHD.Change)chainKey.ChildIndex;
							addressIndex = i;
							return true;
						}
					}

					change = null;
					addressIndex = null;
					return false;
				}
			}

			/// <summary>
			/// Checks whether a given transparent receiver was derived from this key
			/// (in other words: would ZEC sent to this receiver arrive in this account?).
			/// </summary>
			/// <param name="receiver">The receiver to test.</param>
			/// <param name="maxAddressIndex"><inheritdoc cref="TryGetAddressIndex(in TransparentP2PKHReceiver, uint, out Bip44MultiAccountHD.Change?, out uint?)" path="/param[@name='maxAddressIndex']"/></param>
			/// <returns><see langword="true"/> if this receiver would send ZEC to this account; otherwise <see langword="false"/>.</returns>
			/// <remarks>
			/// <para>This is a simpler front-end for the <see cref="TryGetAddressIndex(in TransparentP2SHReceiver, uint, out Bip44MultiAccountHD.Change?, out uint?)"/> method,
			/// which runs a similar test but also provides the change and address index derivations that match the given receiver.</para>
			/// </remarks>
			public bool CheckReceiver(in TransparentP2SHReceiver receiver, uint maxAddressIndex) => this.TryGetAddressIndex(receiver, maxAddressIndex, out _, out _);

			/// <inheritdoc cref="TryGetAddressIndex(in TransparentP2PKHReceiver, uint, out Bip44MultiAccountHD.Change?, out uint?)"/>
			public bool TryGetAddressIndex(in TransparentP2SHReceiver receiver, uint maxAddressIndex, [NotNullWhen(true)] out Bip44MultiAccountHD.Change? change, [NotNullWhen(true)] out uint? addressIndex)
			{
				throw new NotImplementedException();
			}

			/// <inheritdoc/>
			bool IUnifiedEncodingElementEqualityComparer.Equals(IUnifiedEncodingElementEqualityComparer? other)
			{
				return other is ExtendedViewingKey otherKey
					&& this.ChainCode.Equals(otherKey.ChainCode)
					&& this.CryptographicKey.Equals(otherKey.CryptographicKey)
					&& this.Depth == otherKey.Depth;
			}

			/// <inheritdoc/>
			int IUnifiedEncodingElementEqualityComparer.GetHashCode()
			{
				HashCode result = default;
				result.AddBytes(this.ChainCode);
				result.Add(this.CryptographicKey);
				result.Add(this.Depth);
				return result.ToHashCode();
			}

			/// <inheritdoc/>
			public bool Equals(ExtendedViewingKey? other)
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
			public override bool Equals(object? obj) => obj is ExtendedViewingKey other && this.Equals(other);

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
				written += this.ChainCode[..].CopyToRetLength(destination[written..]);
				this.CryptographicKey.WriteToSpan(compressed: true, destination[written..], out int keyLength);
				written += keyLength;
				Assumes.True(written == 65);
				return written;
			}

			/// <inheritdoc cref="Zcash.Orchard.SpendingKey.DecodeUnifiedViewingKeyContribution(ReadOnlySpan{byte}, ZcashNetwork)"/>
			internal static IUnifiedEncodingElement DecodeUnifiedViewingKeyContribution(ReadOnlySpan<byte> keyContribution, ZcashNetwork network, bool isFullViewingKey)
			{
				ReadOnlySpan<byte> chainCode = keyContribution[..32];
				ReadOnlySpan<byte> publicKeyData = keyContribution[32..];

				// We have to assume or bluff on some of these values, since they aren't preserved by the encoding.
				// The values don't actually matter as they don't impact the generated cryptographic key.
				FullViewingKeyTag parentFingerprintTag = default; // we don't know it, but that's OK.
				byte depth = isFullViewingKey ? (byte)3 : (byte)4; // A full viewing key always has a depth of 3.
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
