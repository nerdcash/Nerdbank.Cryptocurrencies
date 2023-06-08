// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Numerics;

namespace Nerdbank.Zcash;

public partial class Zip32HDWallet
{
	public partial class Orchard
	{
		public class ExtendedFullViewingKey : ExtendedKeyBase
		{
			internal ExtendedFullViewingKey(FullViewingKey key, ReadOnlySpan<byte> chainCode, ReadOnlySpan<byte> parentFullViewingKeyTag, byte depth, uint childNumber, bool isTestNet = false)
				: base(chainCode, parentFullViewingKeyTag, depth, childNumber, isTestNet)
			{
				this.Key = key;
			}

			public FullViewingKey Key { get; }

			/// <summary>
			/// Gets the fingerprint for this key.
			/// </summary>
			/// <remarks>
			/// Extended keys can be identified by the Hash160 (RIPEMD160 after SHA256) of the serialized ECDSA public key K, ignoring the chain code.
			/// This corresponds exactly to the data used in traditional Bitcoin addresses.
			/// It is not advised to represent this data in base58 format though, as it may be interpreted as an address that way
			/// (and wallet software is not required to accept payment to the chain key itself).
			/// </remarks>
			public ReadOnlySpan<byte> Fingerprint => throw new NotImplementedException();

			public override ExtendedKeyBase Derive(uint childNumber)
			{
				throw new NotImplementedException();
			}

			/// <summary>
			/// Creates a diversifier (the value of <see cref="OrchardReceiver.D"/>)
			/// for this viewing key.
			/// </summary>
			/// <param name="index">
			/// The index of the diversifier to generate, in the range of 0..(2^88 - 1).
			/// The default diversifier is defined as to be 0.
			/// </param>
			/// <param name="d">Receives the diversifier. Exactly 11 bytes from this span will be initialized.</param>
			/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="index"/> is negative or excees the maximum supported value (2^88-1).</exception>
			/// <remarks>
			/// As specfied by <see href="https://zips.z.cash/zip-0032#orchard-diversifier-derivation">ZIP-0032 Orchard Diversifier Derivation</see>.
			/// </remarks>
			public void GetDiversifier(BigInteger index, Span<byte> d)
			{
				Requires.Range(index >= 0 && index <= MaxDiversifierIndex, nameof(index));

				Span<byte> K = stackalloc byte[32];
				I2LEBSP(this.Key.Rivk, K);

				Span<byte> B = stackalloc byte[64];
				int bBytesWritten = Repr(this.Key.Ak, B);
				bBytesWritten += I2LEBSP(this.Key.Nk, B.Slice(bBytesWritten, 32));

				Span<byte> t = stackalloc byte[65];
				t[0] = 0x82;
				LEBS2OSP(B, t[1..]);
				Span<byte> prfExpandOutput = stackalloc byte[64];
				PRFexpand(K, t, prfExpandOutput);
				Span<byte> dk = prfExpandOutput[..32];

				Span<byte> indexEncoded = stackalloc byte[11];
				I2LEBSP(index, indexEncoded);
				FF1AES256(dk, indexEncoded, d);
			}
		}
	}
}
