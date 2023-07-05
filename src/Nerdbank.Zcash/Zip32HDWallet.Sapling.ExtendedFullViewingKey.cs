// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Nerdbank.Zcash;

public partial class Zip32HDWallet
{
	public partial class Sapling
	{
		public class ExtendedFullViewingKey : ExtendedKeyBase
		{
			internal ExtendedFullViewingKey(FullViewingKey key, in DiversifierKey dk, in ChainCode chainCode, in FullViewingKeyTag parentFullViewingKeyTag, byte depth, uint childNumber, bool isTestNet)
				: base(chainCode, parentFullViewingKeyTag, depth, childNumber, isTestNet)
			{
				this.Key = key;
				this.Dk = dk;
			}

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

			public FullViewingKey Key { get; }

			/// <summary>
			/// Gets the diversifier key.
			/// </summary>
			/// <value>A 32-byte buffer.</value>
			internal DiversifierKey Dk { get; }

			public override ExtendedFullViewingKey Derive(uint childNumber)
			{
				throw new NotImplementedException();
			}

			/// <summary>
			/// Searches for a valid diversifier (the value of <see cref="SaplingReceiver.D"/>)
			/// for this viewing key, starting at a given diversifier index.
			/// </summary>
			/// <param name="index">
			/// The diversifier index to start searching at, in the range of 0..(2^88 - 1).
			/// Not every index will produce a valid diversifier. About half will fail.
			/// The default diversifier is defined as the smallest non-negative index that produces a valid diversifier.
			/// This value will be changed to match the index at which a diversifier was found.
			/// </param>
			/// <param name="d">Receives the diversifier. Exactly 11 bytes from this span will be initialized.</param>
			/// <returns>
			/// <see langword="true"/> if a valid diversifier could be produced at or above the initial value given by <paramref name="index"/>.
			/// <see langword="false"/> if no valid diversifier could be found at or above <paramref name="index"/>.
			/// </returns>
			/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="index"/> is negative.</exception>
			public bool TryFindDiversifier(ref BigInteger index, Span<byte> d)
			{
				Requires.Range(index >= 0, nameof(index));

				for (; index <= MaxDiversifierIndex; index++)
				{
					if (TryGetDiversifier(this.Dk, index, d))
					{
						return true;
					}
				}

				return false;
			}

			/// <summary>
			/// Encodes the extended full viewing key parts to a buffer.
			/// </summary>
			/// <param name="result">The buffer to receive the encoded key. Must be at least 128 bytes in length.</param>
			/// <returns>The number of bytes written to <paramref name="result"/>. Always 128.</returns>
			internal int EncodeExtFVKParts(Span<byte> result)
			{
				int length = 0;
				length += this.Key.Ak.Value.CopyToRetLength(result[length..]);
				length += this.Key.Nk.Value.CopyToRetLength(result[length..]);
				length += this.Key.Ovk.Value.CopyToRetLength(result[length..]);
				length += this.Dk.Value.CopyToRetLength(result[length..]);
				return length;
			}
		}
	}
}
