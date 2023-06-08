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
			private readonly FixedArrays fixedArrays;

			internal ExtendedFullViewingKey(FullViewingKey key, ReadOnlySpan<byte> dk, ReadOnlySpan<byte> chainCode, ReadOnlySpan<byte> parentFullViewingKeyTag, byte depth, uint childNumber, bool isTestNet)
				: base(chainCode, parentFullViewingKeyTag, depth, childNumber, isTestNet)
			{
				this.Key = key;
				this.fixedArrays = new(dk);
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
			internal ReadOnlySpan<byte> Dk => this.fixedArrays.Dk;

			public override ExtendedFullViewingKey Derive(uint childNumber)
			{
				bool childIsHardened = (childNumber & Bip32HDWallet.HardenedBit) != 0;
				if (childIsHardened)
				{
					throw new InvalidOperationException(Strings.CannotDeriveHardenedChildFromPublicKey);
				}

				Span<byte> i = stackalloc byte[64];

				Span<byte> bytes = stackalloc byte[133];
				bytes[0] = 0x12;
				int bytesWritten = 1;
				bytesWritten += this.EncodeExtFVKParts(bytes[bytesWritten..]);
				bytesWritten += I2LEOSP(childNumber, bytes.Slice(bytesWritten, 4));
				PRFexpand(this.ChainCode, bytes[..bytesWritten], i);

				Span<byte> il = i[0..32];
				Span<byte> ir = i[32..];
				Span<byte> expandOutput = stackalloc byte[64];

				PRFexpand(il, new(0x13), expandOutput);
				BigInteger ask = ToScalar(expandOutput);

				PRFexpand(il, new(0x14), expandOutput);
				BigInteger nsk = ToScalar(expandOutput);

				Span<byte> ovk = stackalloc byte[33];
				ovk[0] = 0x15;
				this.Key.Ovk.CopyTo(ovk[1..]);
				PRFexpand(il, ovk, expandOutput);
				expandOutput[..32].CopyTo(ovk);

				Span<byte> dk = stackalloc byte[33];
				dk[0] = 0x16;
				this.Dk.CopyTo(dk[1..]);
				PRFexpand(il, dk, expandOutput);
				expandOutput[..32].CopyTo(dk);

				FullViewingKey key = new(
					ak: G_Sapling.Multiply(ask.ToBouncyCastle()).Add(this.Key.Ak),
					nk: H_Sapling.Multiply(nsk.ToBouncyCastle()).Add(this.Key.Nk),
					ovk: ovk[..32]);

				return new ExtendedFullViewingKey(
					key,
					dk: dk[..32],
					chainCode: ir,
					parentFullViewingKeyTag: this.Fingerprint[..4],
					depth: checked((byte)(this.Depth + 1)),
					childNumber: childNumber,
					isTestNet: this.IsTestNet);
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
				Span<byte> reprOutput = stackalloc byte[32];

				Repr_J(this.Key.Ak, reprOutput);
				length += LEBS2OS(reprOutput, result[..32]);

				Repr_J(this.Key.Nk, reprOutput);
				length += LEBS2OSP(reprOutput, result[32..64]);

				length += this.Key.Ovk.CopyToRetLength(result[length..]);
				length += this.Dk.CopyToRetLength(result[length..]);

				return length;
			}

			private unsafe struct FixedArrays
			{
				private fixed byte dk[32];

				internal FixedArrays(ReadOnlySpan<byte> dk)
				{
					dk.CopyToWithLengthCheck(this.DkWritable);
				}

				internal readonly ReadOnlySpan<byte> Dk => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in this.dk[0]), 32);

				internal Span<byte> DkWritable => MemoryMarshal.CreateSpan(ref this.dk[0], 32);
			}
		}
	}
}
