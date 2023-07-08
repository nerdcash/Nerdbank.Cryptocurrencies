// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Numerics;
using Nerdbank.Zcash.FixedLengthStructs;
using Org.BouncyCastle.Math.EC;

namespace Nerdbank.Zcash;

public partial class Zip32HDWallet
{
	public partial class Orchard
	{
		/// <summary>
		/// A viewing key that can decrypt incoming and outgoing transactions.
		/// </summary>
		public class FullViewingKey : IKey
		{
			private readonly Bytes96 rawEncoding;

			/// <summary>
			/// Initializes a new instance of the <see cref="FullViewingKey"/> class.
			/// </summary>
			/// <param name="spendingKey">The spending key from which to derive the full viewing key.</param>
			internal FullViewingKey(ExtendedSpendingKey spendingKey)
			{
				Span<byte> fvk = stackalloc byte[96];
				if (NativeMethods.TryDeriveOrchardFullViewingKeyFromSpendingKey(spendingKey.SpendingKey.Value, fvk) != 0)
				{
					throw new ArgumentException(Strings.InvalidKey);
				}

				this.rawEncoding = new Bytes96(fvk);
				this.IsTestNet = spendingKey.IsTestNet;
			}

			/// <inheritdoc/>
			public bool IsTestNet { get; }

			/// <summary>
			/// Gets the fingerprint for this key.
			/// </summary>
			public FullViewingKeyFingerprint Fingerprint
			{
				get
				{
					Span<byte> output = stackalloc byte[32];
					Blake2B.ComputeHash(this.rawEncoding.Value, output, new Blake2B.Config { Personalization = "ZcashOrchardFVFP"u8, OutputSizeInBytes = 32 });
					return new(output);
				}
			}

			/// <summary>
			/// Gets the spend validating key.
			/// </summary>
			internal SpendValidatingKey Ak => new(this.rawEncoding.Value[0..32]);

			/// <summary>
			/// Gets the nullifier deriving key.
			/// </summary>
			internal NullifierDerivingKey Nk => new(this.rawEncoding.Value[32..64]);

			/// <summary>
			/// Gets the commit randomness.
			/// </summary>
			internal CommitIvkRandomness Rivk => new(this.rawEncoding.Value[64..]);

			/// <summary>
			/// Gets the first 4 bytes of the fingerprint.
			/// </summary>
			internal FullViewingKeyTag Tag => new(this.Fingerprint.Value[..4]);

			/// <inheritdoc cref="CreateReceiver(ReadOnlySpan{byte})"/>
			public OrchardReceiver CreateReceiver(BigInteger diversifierIndex)
			{
				Span<byte> diversifierSpan = stackalloc byte[11];
				if (!diversifierIndex.TryWriteBytes(diversifierSpan, out _, isUnsigned: true))
				{
					throw new ArgumentOutOfRangeException(nameof(diversifierIndex), "Integer must be representable in 11 bytes.");
				}

				return this.CreateReceiver(diversifierSpan);
			}

			/// <summary>
			/// Creates an orchard receiver using this key and a given diversifier.
			/// </summary>
			/// <param name="diversifierIndex">An 11-byte deterministic diversifier.</param>
			/// <returns>The orchard receiver.</returns>
			public OrchardReceiver CreateReceiver(ReadOnlySpan<byte> diversifierIndex)
			{
				Span<byte> rawReceiver = stackalloc byte[43];
				if (NativeMethods.TryGetOrchardRawPaymentAddress(this.rawEncoding.Value, diversifierIndex, rawReceiver) != 0)
				{
					throw new InvalidKeyException(Strings.InvalidKey);
				}

				return new(rawReceiver);
			}
		}
	}
}
