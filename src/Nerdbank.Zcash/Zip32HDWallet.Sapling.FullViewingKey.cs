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
		/// <summary>
		/// A viewing key that can decrypt incoming and outgoing transactions.
		/// </summary>
		public class FullViewingKey
		{
			/// <summary>
			/// Initializes a new instance of the <see cref="FullViewingKey"/> class.
			/// </summary>
			/// <param name="spendingKey">The spending key from which to derive this full viewing key.</param>
			internal FullViewingKey(in ExpandedSpendingKey spendingKey)
			{
				Span<byte> fvk_bytes = stackalloc byte[96];
				if (NativeMethods.TryGetSaplingFullViewingKeyFromExpandedSpendingKey(spendingKey.ToBytes().Value, fvk_bytes) != 0)
				{
					throw new ArgumentException();
				}

				this.ViewingKey = new(new(fvk_bytes[..32]), new(fvk_bytes[32..64]));
				this.Ovk = new(fvk_bytes[64..96]);
			}

			/// <summary>
			/// Gets the viewing key.
			/// </summary>
			internal ViewingKey ViewingKey { get; }

			internal SubgroupPoint Ak => this.ViewingKey.Ak;

			internal NullifierDerivingKey Nk => this.ViewingKey.Nk;

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
			/// Gets the raw encoding.
			/// </summary>
			/// <param name="rawEncoding">Receives the raw encoding. Must be at least 96 bytes in length.</param>
			/// <returns>The number of bytes written to <paramref name="rawEncoding"/>. Always 96.</returns>
			/// <remarks>
			/// As specified in the <see href="https://zips.z.cash/protocol/protocol.pdf">Zcash protocol spec section 5.6.3.3</see>.
			/// </remarks>
			internal int GetRawEncoding(Span<byte> rawEncoding)
			{
				int written = 0;
				written += this.Ak.Value.CopyToRetLength(rawEncoding[written..]);
				written += this.Nk.Value.CopyToRetLength(rawEncoding[written..]);
				written += this.Ovk.Value.CopyToRetLength(rawEncoding[written..]);
				return written;
			}
		}
	}
}
