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
		public class ExtendedSpendingKey : IExtendedKey
		{
			private FullViewingKey? fullViewingKey;

			/// <summary>
			/// Initializes a new instance of the <see cref="ExtendedSpendingKey"/> class.
			/// </summary>
			/// <param name="spendingKey">The spending key.</param>
			/// <param name="chainCode">The chain code.</param>
			/// <param name="parentFullViewingKeyTag">The tag from the full viewing key. Use the default value if not derived.</param>
			/// <param name="depth">The derivation depth of this key. Use 0 if there is no parent.</param>
			/// <param name="childIndex">The derivation number used to derive this key from its parent. Use 0 if there is no parent.</param>
			/// <param name="network">The network this key should be used with.</param>
			internal ExtendedSpendingKey(in SpendingKey spendingKey, in ChainCode chainCode, in FullViewingKeyTag parentFullViewingKeyTag, byte depth, uint childIndex, ZcashNetwork network = ZcashNetwork.MainNet)
			{
				this.SpendingKey = spendingKey;
				this.ChainCode = chainCode;
				this.ParentFullViewingKeyTag = parentFullViewingKeyTag;
				this.Depth = depth;
				this.ChildIndex = childIndex;
				this.Network = network;
			}

			/// <summary>
			/// Gets the full viewing key.
			/// </summary>
			public FullViewingKey FullViewingKey => this.fullViewingKey ??= this.CreateFullViewingKey();

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
			public ZcashNetwork Network { get; }

			/// <inheritdoc/>
			bool IKey.IsTestNet => this.Network != ZcashNetwork.MainNet;

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
			internal SpendingKey SpendingKey { get; }

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
				PRFexpand(this.ChainCode.Value, PrfExpandCodes.OrchardZip32Child, bytes, i);
				Span<byte> spendingKey = i[0..32];
				ChainCode chainCode = new(i[32..]);

				SpendingKey key = new(spendingKey);
				return new ExtendedSpendingKey(
					key,
					chainCode,
					parentFullViewingKeyTag: GetFingerprint(this.FullViewingKey).Tag,
					depth: checked((byte)(this.Depth + 1)),
					childIndex,
					this.Network);
			}

			/// <inheritdoc/>
			Cryptocurrencies.IExtendedKey Cryptocurrencies.IExtendedKey.Derive(uint childIndex) => this.Derive(childIndex);

			/// <summary>
			/// Initializes a new instance of the <see cref="FullViewingKey"/> class.
			/// </summary>
			private FullViewingKey CreateFullViewingKey()
			{
				Span<byte> fvk = stackalloc byte[96];
				if (NativeMethods.TryDeriveOrchardFullViewingKeyFromSpendingKey(this.SpendingKey.Value, fvk) != 0)
				{
					throw new ArgumentException(Strings.InvalidKey);
				}

				return new(fvk, this.Network);
			}
		}
	}
}
