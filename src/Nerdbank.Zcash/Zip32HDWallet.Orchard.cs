// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Org.BouncyCastle.Math.EC;

namespace Nerdbank.Zcash;

public partial class Zip32HDWallet
{
	internal class Orchard
	{
		internal static ExtendedSpendingKey GenerateMasterKey(ReadOnlySpan<byte> s)
		{
			Span<byte> blakeOutput = stackalloc byte[64]; // 512 bits
			Blake2B.ComputeHash(s, blakeOutput, new Blake2B.Config { Personalization = "ZcashIP32Orchard"u8, OutputSizeInBytes = blakeOutput.Length });

			Span<byte> spendingKey = blakeOutput[..32];
			Span<byte> chainCode = blakeOutput[32..];
			return new(spendingKey, chainCode);
		}

		/// <summary>
		/// Derives a spending key from a given parent key.
		/// </summary>
		/// <param name="parent">The extended spending key from which the returned key will be derived.</param>
		/// <param name="index">The index of the derived child key.</param>
		/// <returns>The derived key.</returns>
		internal static ExtendedSpendingKey DeriveSpendingKey(in ExtendedSpendingKey parent, uint index)
		{
			bool childIsHardened = (index & Bip32HDWallet.HardenedBit) != 0;
			if (!childIsHardened)
			{
				throw new ArgumentException(Strings.OnlyHardenedChildKeysSupported, nameof(index));
			}

			Span<byte> bytes = stackalloc byte[1 + 32 + 4];
			bytes[0] = 0x81;
			int bytesWritten = 1;
			parent.SpendingKey.CopyTo(bytes[bytesWritten..]);
			bytesWritten += parent.SpendingKey.Length;
			bytesWritten += I2LEOSP(index, bytes.Slice(bytesWritten, 4));
			Span<byte> i = stackalloc byte[64];
			PRFexpand(parent.ChainCode, bytes[..bytesWritten], i);
			Span<byte> spendingKey = i[0..32];
			Span<byte> chainCode = i[32..];
			return new(spendingKey, chainCode);
		}

		/// <summary>
		/// Encodes a diversifier (the value of <see cref="OrchardReceiver.D"/>)
		/// for a given <see cref="ExtendedFullViewingKey"/>.
		/// </summary>
		/// <param name="viewingKey">An extended full viewing key.</param>
		/// <param name="index">
		/// The diversifier index, in the range of 0..(2^88 - 1).
		/// Every index will produce a valid diversifier.
		/// The default diversifier is defined as the one with 0 for this value.
		/// </param>
		/// <param name="d">Receives the diversifier. Exactly 88 bytes from this span will be initialized.</param>
		internal static void GetDiversifier(ExtendedFullViewingKey viewingKey, BigInteger index, Span<byte> d)
		{
			Span<byte> k = stackalloc byte[256 / 8];
			I2LEBSP(viewingKey.Rivk, k);
			Span<byte> b = stackalloc byte[64]; // length?
			int bytesWritten = Repr(viewingKey.Ak, b);
			bytesWritten += I2LEBSP(viewingKey.Nk, b[bytesWritten..]);

			Span<byte> prfExpandInput = stackalloc byte[32];
			prfExpandInput[0] = 0x82;
			LEBS2OSP(b[..bytesWritten], prfExpandInput[1..]);
			Span<byte> prfExpandOutput = stackalloc byte[64];
			PRFexpand(k, prfExpandInput[..(1 + bytesWritten)], prfExpandOutput);
			Span<byte> dk = prfExpandOutput[..32];

			Span<byte> indexAsBytes = stackalloc byte[88];
			I2LEBSP(index, indexAsBytes);
			FF1AES256(dk, indexAsBytes, d);
		}

		internal unsafe struct ExtendedSpendingKey
		{
			/// <summary>
			/// Orchard spending key.
			/// </summary>
			private fixed byte sk[32];

			/// <summary>
			/// The chain code.
			/// </summary>
			private fixed byte chainCode[32];

			public ExtendedSpendingKey(ReadOnlySpan<byte> spendingKey, ReadOnlySpan<byte> chainCode)
			{
				if (spendingKey.Length != 32)
				{
					throw new ArgumentException($"Length must be exactly 32, but was {spendingKey.Length}.", nameof(spendingKey));
				}

				if (chainCode.Length != 32)
				{
					throw new ArgumentException($"Length must be exactly 32, but was {chainCode.Length}.", nameof(chainCode));
				}

				spendingKey.CopyTo(this.SpendingKeyWritable);
				chainCode.CopyTo(this.ChainCodeWritable);
			}

			/// <summary>
			/// Gets the spending key. Always 32 bytes in length.
			/// </summary>
			internal readonly ReadOnlySpan<byte> SpendingKey => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in this.sk[0]), 32);

			/// <summary>
			/// Gets the spending key. Always 32 bytes in length.
			/// </summary>
			internal Span<byte> SpendingKeyWritable => MemoryMarshal.CreateSpan(ref this.sk[0], 32);

			/// <summary>
			/// Gets the chain code. Always 32 bytes in length.
			/// </summary>
			internal readonly ReadOnlySpan<byte> ChainCode => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in this.chainCode[0]), 32);

			/// <summary>
			/// Gets the chain code. Always 32 bytes in length.
			/// </summary>
			internal Span<byte> ChainCodeWritable => MemoryMarshal.CreateSpan(ref this.chainCode[0], 32);
		}

		internal unsafe struct ExtendedFullViewingKey
		{
			private readonly ECPoint ak;
			private readonly BigInteger nk;
			private readonly BigInteger rivk;
			private fixed byte chainCode[32];

			public ExtendedFullViewingKey(ECPoint ak, BigInteger nk, BigInteger rivk, ReadOnlySpan<byte> chainCode)
			{
				if (chainCode.Length != 32)
				{
					throw new ArgumentException($"Length must be exactly 32, but was {chainCode.Length}.", nameof(chainCode));
				}

				this.ak = ak;
				this.nk = nk;
				this.rivk = rivk;
				chainCode.CopyTo(this.ChainCodeWritable);
			}

			internal readonly ECPoint Ak => this.ak;

			internal readonly BigInteger Nk => this.nk;

			internal readonly BigInteger Rivk => this.rivk;

			internal readonly ReadOnlySpan<byte> ChainCode => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in this.chainCode[0]), 32);

			internal Span<byte> ChainCodeWritable => MemoryMarshal.CreateSpan(ref this.chainCode[0], 32);
		}
	}
}
