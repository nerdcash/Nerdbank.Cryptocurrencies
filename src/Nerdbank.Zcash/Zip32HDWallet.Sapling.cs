// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Org.BouncyCastle.Math.EC;

namespace Nerdbank.Zcash;

public partial class Zip32HDWallet
{
	/// <summary>
	/// The "Randomness Beacon".
	/// </summary>
	/// <remarks>
	/// The value for this is defined in <see href="https://zips.z.cash/protocol/protocol.pdf">the Zcash protocol</see> §5.9.
	/// </remarks>
	internal static readonly BigInteger URS = BigInteger.Parse("096b36a5804bfacef1691e173c366a47ff5ba84a44f26ddd7e8d9f79d5b42df0", System.Globalization.NumberStyles.HexNumber);

	internal static readonly ECPoint G_Sapling = Curves.JubJub.FindGroupHash("Zcash_G_", string.Empty);

	internal static readonly ECPoint H_Sapling = Curves.JubJub.FindGroupHash("Zcash_H_", string.Empty);

	internal class Sapling
	{
		/// <summary>
		/// Generates a master extended spending key.
		/// </summary>
		/// <param name="s">The seed byte sequence, which MUST be at least 32 and at most 252 bytes.</param>
		/// <returns>The master extended spending key.</returns>
		internal static ExtendedSpendingKey GenerateMasterKey(ReadOnlySpan<byte> s)
		{
			Span<byte> blakeOutput = stackalloc byte[64]; // 512 bits
			Blake2B.ComputeHash(s, blakeOutput, new Blake2B.Config { Personalization = "ZcashIP32Sapling"u8, OutputSizeInBytes = blakeOutput.Length });
			Span<byte> spendingKey = blakeOutput[..32];
			Span<byte> chainCode = blakeOutput[32..];

			Span<byte> expandOutput = stackalloc byte[64];
			PRFexpand(spendingKey, new(0x00), expandOutput);
			BigInteger ask = ToScalar(expandOutput);

			PRFexpand(spendingKey, new(0x01), expandOutput);
			BigInteger nsk = ToScalar(expandOutput);

			PRFexpand(spendingKey, new(0x02), expandOutput);
			Span<byte> ovk = stackalloc byte[32];
			expandOutput[..32].CopyTo(ovk);

			PRFexpand(spendingKey, new(0x10), expandOutput);
			Span<byte> dk = stackalloc byte[32];
			expandOutput[..32].CopyTo(dk);

			return new ExtendedSpendingKey(ask, nsk, ovk, dk, chainCode);
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
			Span<byte> i = stackalloc byte[64];
			if (childIsHardened)
			{
				Span<byte> bytes = stackalloc byte[133];
				bytes[0] = 0x11;
				int bytesWritten = 1;
				bytesWritten += EncodeExtSKParts(parent.Ask, parent.Nsk, parent.Ovk, parent.Dk, bytes[bytesWritten..]);
				bytesWritten += I2LEOSP(index, bytes.Slice(bytesWritten, 4));
				PRFexpand(parent.ChainCode, bytes[..bytesWritten], i);
			}
			else
			{
				Span<byte> bytes = stackalloc byte[133];
				bytes[0] = 0x12;
				int bytesWritten = 1;
				throw new NotImplementedException(); // ak, nk derivations are complex.
				ECPoint ak = default;
				ECPoint nk = default;
				bytesWritten += EncodeExtFVKParts(ak, nk, parent.Ovk, parent.Dk, bytes[bytesWritten..]);
				bytesWritten += I2LEOSP(index, bytes.Slice(bytesWritten, 4));
				PRFexpand(parent.ChainCode, bytes[..bytesWritten], i);
			}

			Span<byte> il = i[0..32];
			Span<byte> ir = i[32..];
			Span<byte> expandOutput = stackalloc byte[64];

			PRFexpand(il, new(0x13), expandOutput);
			BigInteger ask = ToScalar(expandOutput);

			PRFexpand(il, new(0x14), expandOutput);
			BigInteger nsk = ToScalar(expandOutput);

			Span<byte> ovk = stackalloc byte[33];
			ovk[0] = 0x15;
			parent.Ovk.CopyTo(ovk[1..]);
			PRFexpand(il, ovk, expandOutput);
			expandOutput[..32].CopyTo(ovk);

			Span<byte> dk = stackalloc byte[33];
			dk[0] = 0x16;
			parent.Dk.CopyTo(dk[1..]);
			PRFexpand(il, dk, expandOutput);
			expandOutput[..32].CopyTo(dk);

			return new ExtendedSpendingKey(
				ask: BigInteger.Remainder(ask + parent.Ask, Curves.JubJub.Order),
				nsk: BigInteger.Remainder(nsk - parent.Nsk, Curves.JubJub.Order),
				ovk: ovk[..32],
				dk: dk[..32],
				ir);
		}

		internal static ExtendedFullViewingKey DeriveFullViewingKey(ExtendedFullViewingKey parent, uint index)
		{
			bool childIsHardened = (index & Bip32HDWallet.HardenedBit) != 0;
			if (childIsHardened)
			{
				throw new InvalidOperationException(Strings.CannotDeriveHardenedChildFromPublicKey);
			}

			Span<byte> i = stackalloc byte[64];

			Span<byte> bytes = stackalloc byte[133];
			bytes[0] = 0x12;
			int bytesWritten = 1;
			bytesWritten += EncodeExtFVKParts(parent.Ak, parent.Nk, parent.Ovk, parent.Dk, bytes[bytesWritten..]);
			bytesWritten += I2LEOSP(index, bytes.Slice(bytesWritten, 4));
			PRFexpand(parent.ChainCode, bytes[..bytesWritten], i);

			Span<byte> il = i[0..32];
			Span<byte> ir = i[32..];
			Span<byte> expandOutput = stackalloc byte[64];

			PRFexpand(il, new(0x13), expandOutput);
			BigInteger ask = ToScalar(expandOutput);

			PRFexpand(il, new(0x14), expandOutput);
			BigInteger nsk = ToScalar(expandOutput);

			Span<byte> ovk = stackalloc byte[33];
			ovk[0] = 0x15;
			parent.Ovk.CopyTo(ovk[1..]);
			PRFexpand(il, ovk, expandOutput);
			expandOutput[..32].CopyTo(ovk);

			Span<byte> dk = stackalloc byte[33];
			dk[0] = 0x16;
			parent.Dk.CopyTo(dk[1..]);
			PRFexpand(il, dk, expandOutput);
			expandOutput[..32].CopyTo(dk);

			return new ExtendedFullViewingKey(
				ak: G_Sapling.Multiply(ask.ToBouncyCastle()).Add(parent.Ak),
				nk: H_Sapling.Multiply(nsk.ToBouncyCastle()).Add(parent.Nk),
				ovk: ovk[..32],
				dk: dk[..32],
				ir);
		}

		private static BigInteger ToScalar(ReadOnlySpan<byte> x)
		{
			return BigInteger.Remainder(LEOS2IP(x), Curves.JubJub.Order);
		}

		/// <summary>
		/// Encodes an extended spending key to a buffer.
		/// </summary>
		/// <param name="ask"></param>
		/// <param name="nsk"></param>
		/// <param name="ovk"></param>
		/// <param name="dk"></param>
		/// <param name="result">The buffer to write the encoded key to.</param>
		/// <returns>The number of bytes written to <paramref name="result"/>. Always 128.</returns>
		internal static int EncodeExtSKParts(
			BigInteger ask,
			BigInteger nsk,
			ReadOnlySpan<byte> ovk,
			ReadOnlySpan<byte> dk,
			Span<byte> result)
		{
			if (ovk.Length != 32)
			{
				throw new ArgumentException(Strings.FormatUnexpectedLength(32, ovk.Length), nameof(ovk));
			}

			if (dk.Length != 32)
			{
				throw new ArgumentException(Strings.FormatUnexpectedLength(32, dk.Length), nameof(dk));
			}

			int bytesWritten = 0;

			I2LEOSP(ask, result[bytesWritten..32]);
			bytesWritten += 32;

			I2LEOSP(nsk, result[bytesWritten..32]);
			bytesWritten += 32;

			ovk.CopyTo(result[bytesWritten..]);
			bytesWritten += ovk.Length; // +32

			dk.CopyTo(result[bytesWritten..]);
			bytesWritten += dk.Length; // +32

			return bytesWritten;
		}

		internal static int EncodeExtFVKParts(
			ECPoint ak,
			ECPoint nk,
			ReadOnlySpan<byte> ovk,
			ReadOnlySpan<byte> dk,
			Span<byte> result)
		{
			// TODO: implement this.
			// This requires the EC function: reprJ
			throw new NotImplementedException();
		}

		/// <summary>
		/// Encodes a diversifier (the value of <see cref="SaplingReceiver.D"/>)
		/// for a given diversifier key (the value of <see cref="ExtendedFullViewingKey.Dk"/>).
		/// </summary>
		/// <param name="dk">A 32-byte buffer containing the diversifier key.</param>
		/// <param name="index">
		/// The diversifier index, in the range of 0..(2^88 - 1).
		/// Not every index will produce a valid diversifier. About half will fail.
		/// The default diversifier is defined as the smallest non-negative index that produces a valid diversifier.
		/// </param>
		/// <param name="d">Receives the diversifier. Exactly 88 bytes from this span will be initialized.</param>
		/// <returns>
		/// <see langword="true"/> if a valid diversifier could be produced with the given <paramref name="index"/>.
		/// <see langword="false"/> if the caller should retry with the next higher index.
		/// </returns>
		internal static bool TryGetDiversifier(ReadOnlySpan<byte> dk, BigInteger index, Span<byte> d)
		{
			Span<byte> indexAsBytes = stackalloc byte[88];
			I2LEBSP(index, indexAsBytes);
			FF1AES256(dk, indexAsBytes, d);
			return !DiversifyHash(indexAsBytes).IsInfinity;
		}

		/// <summary>
		/// Maps a diversifier to a base point on the JubJub elliptic curve, or to ⊥ if the diversifier is invalid.
		/// </summary>
		/// <param name="d">The diversifier.</param>
		/// <returns>A point on the JubJub elliptic curve.</returns>
		private static ECPoint DiversifyHash(ReadOnlySpan<byte> d)
		{
			throw new NotImplementedException();
		}

		internal unsafe struct ExtendedSpendingKey
		{
			private readonly BigInteger ask;
			private readonly BigInteger nsk;
			private fixed byte ovk[32];
			private fixed byte dk[32];
			private fixed byte chainCode[32];

			public ExtendedSpendingKey(BigInteger ask, BigInteger nsk, ReadOnlySpan<byte> ovk, ReadOnlySpan<byte> dk, ReadOnlySpan<byte> chainCode)
			{
				if (ovk.Length != 32)
				{
					throw new ArgumentException($"Length must be exactly 32, but was {ovk.Length}.", nameof(ovk));
				}

				if (dk.Length != 32)
				{
					throw new ArgumentException($"Length must be exactly 32, but was {dk.Length}.", nameof(dk));
				}

				if (chainCode.Length != 32)
				{
					throw new ArgumentException($"Length must be exactly 32, but was {chainCode.Length}.", nameof(chainCode));
				}

				if (ask == 0)
				{
					throw new ArgumentException(Strings.InvalidKey);
				}

				this.ask = ask;
				this.nsk = nsk;
				ovk.CopyTo(this.OvkWritable);
				dk.CopyTo(this.DkWritable);
				chainCode.CopyTo(this.ChainCodeWritable);
			}

			internal BigInteger Ask => this.ask;

			internal BigInteger Nsk => this.nsk;

			internal readonly ReadOnlySpan<byte> Ovk => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in this.ovk[0]), 32);

			internal Span<byte> OvkWritable => MemoryMarshal.CreateSpan(ref this.ovk[0], 32);

			internal readonly ReadOnlySpan<byte> Dk => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in this.dk[0]), 32);

			internal Span<byte> DkWritable => MemoryMarshal.CreateSpan(ref this.dk[0], 32);

			internal readonly ReadOnlySpan<byte> ChainCode => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in this.chainCode[0]), 32);

			internal Span<byte> ChainCodeWritable => MemoryMarshal.CreateSpan(ref this.chainCode[0], 32);
		}

		internal unsafe struct ExtendedFullViewingKey
		{
			private readonly ECPoint ak;
			private readonly ECPoint nk;
			private fixed byte ovk[32];
			private fixed byte dk[32];
			private fixed byte chainCode[32];

			public ExtendedFullViewingKey(ECPoint ak, ECPoint nk, ReadOnlySpan<byte> ovk, ReadOnlySpan<byte> dk, ReadOnlySpan<byte> chainCode)
			{
				if (ovk.Length != 32)
				{
					throw new ArgumentException($"Length must be exactly 32, but was {ovk.Length}.", nameof(ovk));
				}

				if (dk.Length != 32)
				{
					throw new ArgumentException($"Length must be exactly 32, but was {dk.Length}.", nameof(dk));
				}

				if (chainCode.Length != 32)
				{
					throw new ArgumentException($"Length must be exactly 32, but was {chainCode.Length}.", nameof(chainCode));
				}

				if (!ak.IsValid()) // TODO: Does this include a zero point check?
				{
					throw new ArgumentException(Strings.InvalidKey);
				}

				this.ak = ak;
				this.nk = nk;
				ovk.CopyTo(this.OvkWritable);
				dk.CopyTo(this.DkWritable);
				chainCode.CopyTo(this.ChainCodeWritable);
			}

			internal ECPoint Ak => this.ak;

			internal ECPoint Nk => this.nk;

			internal readonly ReadOnlySpan<byte> Ovk => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in this.ovk[0]), 32);

			internal Span<byte> OvkWritable => MemoryMarshal.CreateSpan(ref this.ovk[0], 32);

			internal readonly ReadOnlySpan<byte> Dk => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in this.dk[0]), 32);

			internal Span<byte> DkWritable => MemoryMarshal.CreateSpan(ref this.dk[0], 32);

			internal readonly ReadOnlySpan<byte> ChainCode => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in this.chainCode[0]), 32);

			internal Span<byte> ChainCodeWritable => MemoryMarshal.CreateSpan(ref this.chainCode[0], 32);
		}
	}
}
