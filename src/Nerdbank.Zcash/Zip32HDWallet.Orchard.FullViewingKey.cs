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
		public class FullViewingKey
		{
			private readonly Bytes96 rawEncoding;

			internal FullViewingKey(SpendingKey spendingKey)
			{
				Span<byte> fvk = stackalloc byte[96];
				if (NativeMethods.TryDeriveOrchardFullViewingKeyFromSpendingKey(spendingKey.Value, fvk) != 0)
				{
					throw new ArgumentException(Strings.InvalidKey);
				}

				this.rawEncoding = new Bytes96(fvk);
			}

			/// <summary>
			/// Creates an orchard receiver using this key and a given diversifier.
			/// </summary>
			/// <param name="diversifier">A 11-byte buffer used as a deterministic diversifier.</param>
			/// <returns>The orchard receiver.</returns>
			public OrchardReceiver CreateReceiver(ulong diversifier)
			{
				Span<byte> rawReceiver = stackalloc byte[43];
				if (NativeMethods.TryGetOrchardRawPaymentAddress(this.rawEncoding.Value, diversifier, rawReceiver) != 0)
				{
					throw new InvalidKeyException(Strings.InvalidKey);
				}

				return new(rawReceiver);
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
			/// Gets the fingerprint for this key.
			/// </summary>
			internal FullViewingKeyFingerprint Fingerprint
			{
				get
				{
					Span<byte> output = stackalloc byte[32];
					Blake2B.ComputeHash(this.rawEncoding.Value, output, new Blake2B.Config { Personalization = "ZcashOrchardFVFP"u8, OutputSizeInBytes = 32 });
					return new(output);
				}
			}

			/// <summary>
			/// Gets the first 4 bytes of the fingerprint.
			/// </summary>
			internal FullViewingKeyTag Tag => new(this.Fingerprint.Value[..4]);
		}
	}
}
