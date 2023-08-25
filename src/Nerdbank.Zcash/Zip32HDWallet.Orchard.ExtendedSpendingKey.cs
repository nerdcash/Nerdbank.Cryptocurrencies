// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Zcash.Orchard;

namespace Nerdbank.Zcash;

public partial class Zip32HDWallet
{
	public partial class Orchard
	{
		/// <summary>
		/// A key capable of spending, extended so it can be used to derive child keys.
		/// </summary>
		[DebuggerDisplay($"{{{nameof(DefaultAddress)},nq}}")]
		public class ExtendedSpendingKey : IExtendedKey, ISpendingKey, IEquatable<ExtendedSpendingKey>
		{
			private const string Bech32MainNetworkHRP = "secret-orchard-extsk-main";
			private const string Bech32TestNetworkHRP = "secret-orchard-extsk-test";

			/// <summary>
			/// Initializes a new instance of the <see cref="ExtendedSpendingKey"/> class.
			/// </summary>
			/// <param name="spendingKey">The spending key.</param>
			/// <param name="chainCode">The chain code.</param>
			/// <param name="parentFullViewingKeyTag">The tag from the full viewing key. Use the default value if not derived.</param>
			/// <param name="depth">The derivation depth of this key. Use 0 if there is no parent.</param>
			/// <param name="childIndex">The derivation number used to derive this key from its parent. Use 0 if there is no parent.</param>
			internal ExtendedSpendingKey(in SpendingKey spendingKey, in ChainCode chainCode, in FullViewingKeyTag parentFullViewingKeyTag, byte depth, uint childIndex)
			{
				this.SpendingKey = spendingKey;
				this.ChainCode = chainCode;
				this.ParentFullViewingKeyTag = parentFullViewingKeyTag;
				this.Depth = depth;
				this.ChildIndex = childIndex;
			}

			/// <summary>
			/// Gets the full viewing key.
			/// </summary>
			public FullViewingKey FullViewingKey => this.SpendingKey.FullViewingKey;

			/// <inheritdoc/>
			IFullViewingKey ISpendingKey.FullViewingKey => this.FullViewingKey;

			/// <summary>
			/// Gets the incoming viewing key.
			/// </summary>
			public IncomingViewingKey IncomingViewingKey => this.FullViewingKey.IncomingViewingKey;

			/// <inheritdoc/>
			IIncomingViewingKey IFullViewingKey.IncomingViewingKey => this.IncomingViewingKey;

			/// <summary>
			/// Gets the fingerprint for this key.
			/// </summary>
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
			public ZcashNetwork Network => this.SpendingKey.Network;

			/// <summary>
			/// Gets the Bech32 encoding of the spending key.
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
			/// Create additional diversified addresses using <see cref="FullViewingKey.CreateReceiver(System.Numerics.BigInteger)"/> found on the <see cref="FullViewingKey"/> property.
			/// </remarks>
			/// <seealso cref="FullViewingKey.CreateDefaultReceiver"/>
			/// <seealso cref="FullViewingKey.CreateReceiver(System.Numerics.BigInteger)"/>
			public OrchardAddress DefaultAddress => new(this.FullViewingKey.CreateDefaultReceiver(), this.Network);

			/// <summary>
			/// Gets the spending key itself.
			/// </summary>
			public SpendingKey SpendingKey { get; }

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

			/// <inheritdoc/>
			public bool Equals(ExtendedSpendingKey? other)
			{
				return other is not null
					&& this.SpendingKey.Value.SequenceEqual(other.SpendingKey.Value)
					&& this.ChainCode.Value.SequenceEqual(other.ChainCode.Value)
					&& this.ParentFullViewingKeyTag.Value.SequenceEqual(other.ParentFullViewingKeyTag.Value)
					&& this.Depth == other.Depth
					&& this.ChildIndex == other.ChildIndex
					&& this.Network == other.Network;
			}

			/// <inheritdoc cref="Cryptocurrencies.IExtendedKey.Derive(uint)"/>
			public ExtendedSpendingKey Derive(uint childIndex)
			{
				bool childIsHardened = (childIndex & Bip32HDWallet.HardenedBit) != 0;
				if (!childIsHardened)
				{
					throw new ArgumentException(Strings.OnlyHardenedChildKeysSupported, nameof(childIndex));
				}

				Span<byte> bytes = stackalloc byte[32 + 4];
				int bytesWritten = 0;
				bytesWritten += this.SpendingKey.Value.CopyToRetLength(bytes);
				bytesWritten += I2LEOSP(childIndex, bytes.Slice(bytesWritten, 4));
				Span<byte> i = stackalloc byte[64];
				ZcashUtilities.PRFexpand(this.ChainCode.Value, PrfExpandCodes.OrchardZip32Child, bytes, i);
				Span<byte> spendingKey = i[0..32];
				ChainCode chainCode = new(i[32..]);

				SpendingKey key = new(spendingKey, this.Network);
				return new ExtendedSpendingKey(
					key,
					chainCode,
					parentFullViewingKeyTag: GetFingerprint(this.FullViewingKey).Tag,
					depth: checked((byte)(this.Depth + 1)),
					childIndex);
			}

			/// <inheritdoc/>
			Cryptocurrencies.IExtendedKey Cryptocurrencies.IExtendedKey.Derive(uint childIndex) => this.Derive(childIndex);

			/// <inheritdoc/>
			IExtendedKey IExtendedKey.Derive(uint childIndex) => this.Derive(childIndex);

			private static ExtendedSpendingKey Decode(ReadOnlySpan<byte> encoded, ZcashNetwork network)
			{
				byte depth = (byte)LEOS2IP(encoded[0..1]);
				ReadOnlySpan<byte> parentFvkTag = encoded[1..5];
				uint childIndex = (uint)LEOS2IP(encoded[5..9]);
				ReadOnlySpan<byte> chainCode = encoded[9..41];
				ReadOnlySpan<byte> spendingKey = encoded[41..73];
				return new ExtendedSpendingKey(
					new SpendingKey(spendingKey, network),
					new ChainCode(chainCode),
					new FullViewingKeyTag(parentFvkTag),
					depth,
					childIndex);
			}

			/// <summary>
			/// Writes the raw encoding of the extended spending key to a given buffer.
			/// </summary>
			/// <param name="result">The buffer to write to, which must be at least 73 bytes.</param>
			/// <returns>The number of bytes written. Always 73.</returns>
			private int Encode(Span<byte> result)
			{
				int length = 0;
				length += I2LEOSP(this.Depth, result[length..]);
				length += this.ParentFullViewingKeyTag.Value.CopyToRetLength(result[length..]);
				length += I2LEOSP(this.ChildIndex, result[length..]);
				length += this.ChainCode.Value.CopyToRetLength(result[length..]);
				length += this.SpendingKey.Value.CopyToRetLength(result[length..]);
				Assumes.True(length == 73);
				return length;
			}
		}
	}
}
