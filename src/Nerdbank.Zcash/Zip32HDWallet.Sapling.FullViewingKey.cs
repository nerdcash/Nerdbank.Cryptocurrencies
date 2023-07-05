// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Org.BouncyCastle.Math.EC;

namespace Nerdbank.Zcash;

public partial class Zip32HDWallet
{

	public partial class Sapling
	{
		public class FullViewingKey
		{
			private readonly ECPoint ak;
			private readonly ECPoint nk;

			internal FullViewingKey(ECPoint ak, ECPoint nk, OutgoingViewingKey ovk)
			{
				if (!ak.IsValid()) // TODO: Does this include a zero point check?
				{
					throw new ArgumentException(Strings.InvalidKey);
				}

				this.ak = ak;
				this.nk = nk;
				this.Ovk = ovk;
			}

			internal ECPoint Ak => this.ak;

			internal ECPoint Nk => this.nk;

			/// <summary>
			/// Gets the outgoing viewing key.
			/// </summary>
			internal OutgoingViewingKey Ovk { get; }

			/// <summary>
			/// Gets the fingerprint for the full viewing key.
			/// </summary>
			internal FullViewingKeyFingerprint Fingerprint
			{
				get
				{
					Span<byte> fingerprint = stackalloc byte[32];
					Span<byte> fvk = stackalloc byte[96];
					this.GetRawEncoding(fvk);
					Blake2B.ComputeHash(fvk, fingerprint, new Blake2B.Config { Personalization = "ZcashSaplingFVFP"u8, OutputSizeInBytes = 32 });
					return new(fingerprint);
				}
			}

			/// <summary>
			/// Gets the first 4 bytes of the fingerprint.
			/// </summary>
			internal FullViewingKeyTag Tag => new(this.Fingerprint.Value[..4]);

			/// <summary>
			/// Creates a sapling receiver using this key and a given diversifier.
			/// </summary>
			/// <param name="diversifier">A deterministic diversifier.</param>
			/// <param name="receiver">Receives the sapling receiver, if successful.</param>
			/// <returns>
			/// A value indicating whether creation of the receiver was successful.
			/// Approximately half of the diversifier values will fail.
			/// Callers should increment the diversifier and retry when they fail.
			/// </returns>
			public bool TryCreateReceiver(ulong diversifier, out SaplingReceiver receiver)
			{
				throw new NotImplementedException();
			}

			/// <summary>
			/// Gets the raw encoding.
			/// </summary>
			/// <param name="rawEncoding">Receives the raw encoding. Must be at least 96 bytes in length.</param>
			/// <returns>The number of bytes written to <paramref name="rawEncoding"/>. Always 96.</returns>
			/// <remarks>
			/// As specified in the <see href="https://zips.z.cash/protocol/protocol.pdf">Zcash protocol spec section 5.6.3.3</see>.
			/// </remarks>
			private int GetRawEncoding(Span<byte> rawEncoding)
			{
				Span<byte> reprOutput = stackalloc byte[32];
				int written = 0;

				Repr_J(this.Ak, reprOutput);
				written += LEBS2OSP(reprOutput, rawEncoding[..32]);

				Repr_J(this.Nk, reprOutput);
				written += LEBS2OSP(reprOutput[..written], rawEncoding[32..64]);

				written += this.Ovk.Value.CopyToRetLength(rawEncoding[64..]);

				return written;
			}
		}
	}
}
