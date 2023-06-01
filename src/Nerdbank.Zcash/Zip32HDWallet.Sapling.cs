// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Nerdbank.Cryptocurrencies;

namespace Nerdbank.Zcash;

public partial class Zip32HDWallet
{
	internal class Sapling
	{
		/// <summary>
		/// Generates a master extended spending key.
		/// </summary>
		/// <param name="s">The seed byte sequence, which MUST be at least 32 and at most 252 bytes.</param>
		/// <returns>The master extended spending key.</returns>
		internal static MasterExtendedSpendingKey MasterKeyGeneration(ReadOnlySpan<byte> s)
		{
			Span<byte> blakeOutput = stackalloc byte[64]; // 512 bits
			Blake2B.ComputeHash(s, blakeOutput, new Blake2B.Config { Personalization = "ZcashIP32Sapling"u8, OutputSizeInBytes = blakeOutput.Length });
			Span<byte> spendingKey = blakeOutput[..32];
			Span<byte> chainCode = blakeOutput[32..];

			Span<byte> expandOutput = stackalloc byte[64];
			Span<byte> appendedByte = stackalloc byte[1];
			appendedByte[0] = 0;
			PRFexpand(spendingKey, appendedByte, expandOutput);
			BigInteger ask = ToScalar(expandOutput);

			appendedByte[0] = 0x01;
			PRFexpand(spendingKey, appendedByte, expandOutput);
			BigInteger nsk = ToScalar(expandOutput);

			appendedByte[0] = 0x02;
			PRFexpand(spendingKey, appendedByte, expandOutput);
			Span<byte> ovk = stackalloc byte[32];
			expandOutput[..32].CopyTo(ovk);

			appendedByte[0] = 0x10;
			PRFexpand(spendingKey, appendedByte, expandOutput);
			Span<byte> dk = stackalloc byte[32];
			expandOutput[..32].CopyTo(dk);

			return new MasterExtendedSpendingKey(ask, nsk, ovk, dk, chainCode);
		}

		private static BigInteger ToScalar(ReadOnlySpan<byte> x)
		{
			return BigInteger.Remainder(LEOS2IP(x), Curves.JubJub.Order);
		}

		internal static int EncodeExtSKParts(
			BigInteger ask,
			BigInteger nsk,
			ReadOnlySpan<byte> ovk,
			ReadOnlySpan<byte> dk,
			Span<byte> result)
		{
			int bytesWritten = 0;

			I2LEOSP(ask, result[bytesWritten..32]);
			bytesWritten += 32;

			I2LEOSP(nsk, result[bytesWritten..32]);
			bytesWritten += 32;

			ovk.CopyTo(result[bytesWritten..]);
			bytesWritten += ovk.Length;

			dk.CopyTo(result[bytesWritten..]);
			bytesWritten += dk.Length;

			return bytesWritten;
		}

		internal static void EncodeExtFVKParts(
			ReadOnlySpan<byte> ak,
			ReadOnlySpan<byte> nk,
			ReadOnlySpan<byte> ovk,
			ReadOnlySpan<byte> dk,
			Span<byte> result)
		{
			// TODO: implement this.
			// This requires the EC function: reprJ
		}

		internal unsafe struct MasterExtendedSpendingKey
		{
			private readonly BigInteger ask;
			private readonly BigInteger nsk;
			private fixed byte ovk[32];
			private fixed byte dk[32];
			private fixed byte chain[32];

			public MasterExtendedSpendingKey(BigInteger ask, BigInteger nsk, ReadOnlySpan<byte> ovk, ReadOnlySpan<byte> dk, ReadOnlySpan<byte> chain)
			{
				if (ovk.Length != 32)
				{
					throw new ArgumentException($"Length must be exactly 32, but was {ovk.Length}.", nameof(ovk));
				}

				if (dk.Length != 32)
				{
					throw new ArgumentException($"Length must be exactly 32, but was {dk.Length}.", nameof(dk));
				}

				if (chain.Length != 32)
				{
					throw new ArgumentException($"Length must be exactly 32, but was {chain.Length}.", nameof(chain));
				}

				this.ask = ask;
				this.nsk = nsk;
				ovk.CopyTo(this.OvkWritable);
				dk.CopyTo(this.DkWritable);
				chain.CopyTo(this.ChainWritable);
			}

			internal BigInteger Ask => this.ask;

			internal BigInteger Nsk => this.nsk;

			internal readonly ReadOnlySpan<byte> Ovk => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in this.ovk[0]), 32);

			internal Span<byte> OvkWritable => MemoryMarshal.CreateSpan(ref this.ovk[0], 32);

			internal readonly ReadOnlySpan<byte> Dk => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in this.dk[0]), 32);

			internal Span<byte> DkWritable => MemoryMarshal.CreateSpan(ref this.dk[0], 32);

			internal readonly ReadOnlySpan<byte> Chain => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in this.chain[0]), 32);

			internal Span<byte> ChainWritable => MemoryMarshal.CreateSpan(ref this.chain[0], 32);
		}
	}
}
