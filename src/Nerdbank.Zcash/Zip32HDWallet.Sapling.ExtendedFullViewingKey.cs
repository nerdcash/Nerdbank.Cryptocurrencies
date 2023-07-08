// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Numerics;

namespace Nerdbank.Zcash;

public partial class Zip32HDWallet
{
	public partial class Sapling
	{
		/// <summary>
		/// The full viewing key, extended so it can be used to derive child keys.
		/// </summary>
		public class ExtendedFullViewingKey : ExtendedKeyBase
		{
			/// <summary>
			/// Initializes a new instance of the <see cref="ExtendedFullViewingKey"/> class.
			/// </summary>
			/// <param name="spendingKey">The spending key from which to derive the full viewing key.</param>
			internal ExtendedFullViewingKey(ExtendedSpendingKey spendingKey)
				: base(spendingKey.ChainCode, spendingKey.ParentFullViewingKeyTag, spendingKey.Depth, spendingKey.ChildNumber, spendingKey.IsTestNet)
			{
				this.Key = new(spendingKey.ExpandedSpendingKey);
				this.Dk = spendingKey.Dk;
			}

			/// <summary>
			/// Initializes a new instance of the <see cref="ExtendedFullViewingKey"/> class.
			/// </summary>
			/// <param name="key">The full viewing key.</param>
			/// <param name="dk">The diversifier key.</param>
			/// <param name="chainCode"><inheritdoc cref="ExtendedKeyBase(in ChainCode, in FullViewingKeyTag, byte, uint, bool)" path="/param[@name='chainCode']"/></param>
			/// <param name="parentFullViewingKeyTag"><inheritdoc cref="ExtendedKeyBase(in ChainCode, in FullViewingKeyTag, byte, uint, bool)" path="/param[@name='parentFullViewingKeyTag']"/></param>
			/// <param name="depth"><inheritdoc cref="ExtendedKeyBase(in ChainCode, in FullViewingKeyTag, byte, uint, bool)" path="/param[@name='depth']"/></param>
			/// <param name="childNumber"><inheritdoc cref="ExtendedKeyBase(in ChainCode, in FullViewingKeyTag, byte, uint, bool)" path="/param[@name='childNumber']"/></param>
			/// <param name="isTestNet"><inheritdoc cref="ExtendedKeyBase(in ChainCode, in FullViewingKeyTag, byte, uint, bool)" path="/param[@name='isTestNet']"/></param>
			internal ExtendedFullViewingKey(FullViewingKey key, DiversifierKey dk, in ChainCode chainCode, in FullViewingKeyTag parentFullViewingKeyTag, byte depth, uint childNumber, bool isTestNet = false)
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
			public ReadOnlySpan<byte> Fingerprint => this.Key.Fingerprint.Value;

			/// <summary>
			/// Gets the full viewing key.
			/// </summary>
			public FullViewingKey Key { get; }

			/// <summary>
			/// Gets the diversifier key.
			/// </summary>
			/// <value>A 32-byte buffer.</value>
			internal DiversifierKey Dk { get; }

			/// <inheritdoc/>
			public override ExtendedFullViewingKey Derive(uint childNumber)
			{
				Span<byte> selfAsBytes = stackalloc byte[169];
				Span<byte> childAsBytes = stackalloc byte[169];
				this.Encode(selfAsBytes);
				if (NativeMethods.DeriveSaplingChildFullViewingKey(selfAsBytes, childNumber, childAsBytes) != 0)
				{
					throw new InvalidKeyException();
				}

				return Decode(childAsBytes, this.IsTestNet);
			}

			/// <summary>
			/// Creates a sapling receiver using this key and a given diversifier.
			/// </summary>
			/// <param name="index">
			/// The diversifier index to start searching at, in the range of 0..(2^88 - 1).
			/// Not every index will produce a valid diversifier. About half will fail.
			/// The default diversifier is defined as the smallest non-negative index that produces a valid diversifier.
			/// This value will be changed to match the index at which a diversifier was found.
			/// </param>
			/// <param name="receiver">Receives the sapling receiver, if successful.</param>
			/// <returns>
			/// <see langword="true"/> if a valid diversifier could be produced at or above the initial value given by <paramref name="index"/>.
			/// <see langword="false"/> if no valid diversifier could be found at or above <paramref name="index"/>.
			/// </returns>
			/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="index"/> is negative.</exception>
			public bool TryCreateReceiver(ref BigInteger index, out SaplingReceiver receiver)
			{
				Requires.Range(index >= 0, nameof(index));

				Span<byte> indexBytes = stackalloc byte[11];
				if (!index.TryWriteBytes(indexBytes, out _, isUnsigned: true))
				{
					throw new ArgumentException("Index must fit within 11 bytes.");
				}

				Span<byte> fvk = stackalloc byte[96];
				this.Key.ToBytes(fvk);

				Span<byte> receiverBytes = stackalloc byte[SaplingReceiver.Length];
				if (NativeMethods.TryGetSaplingReceiver(fvk, this.Dk.Value, indexBytes, receiverBytes) != 0)
				{
					return false;
				}

				// The index may have been changed. Apply that change to our ref parameter.
				index = new BigInteger(indexBytes, isUnsigned: true);

				receiver = new(receiverBytes);

				return true;
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

			private static ExtendedFullViewingKey Decode(ReadOnlySpan<byte> encoded, bool isTestNet)
			{
				byte depth = encoded[0];
				FullViewingKeyTag parentFullViewingKeyTag = new(encoded[1..5]);
				uint childNumber = BitUtilities.ReadUInt32LE(encoded[5..9]);
				ChainCode chainCode = new(encoded[9..41]);
				FullViewingKey fvk = FullViewingKey.FromBytes(encoded[41..137]);
				DiversifierKey dk = new(encoded[137..169]);
				return new(fvk, dk, chainCode, parentFullViewingKeyTag, depth, childNumber, isTestNet);
			}

			/// <summary>
			/// Encodes the entire extended spending key.
			/// </summary>
			/// <param name="result">A buffer of at least 169 bytes in length.</param>
			/// <returns>The number of bytes written. Always 169.</returns>
			/// <remarks>
			/// This is designed to exactly match how rust encodes the extended full viewing key so we can exchange the data.
			/// </remarks>
			private int Encode(Span<byte> result)
			{
				int length = 0;
				result[length++] = this.Depth;
				length += this.ParentFullViewingKeyTag.Value.CopyToRetLength(result[length..]);
				length += BitUtilities.WriteLE(this.ChildNumber, result[length..]);
				length += this.ChainCode.Value.CopyToRetLength(result[length..]);
				length += this.Key.ToBytes(result[length..]);
				length += this.Dk.Value.CopyToRetLength(result[length..]);
				Assumes.True(length == 169);
				return length;
			}
		}
	}
}
