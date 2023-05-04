// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Nerdbank.Cryptocurrencies;

/// <summary>
/// Implements Hierarchical Deterministic Wallets as defined in
/// <see href="https://github.com/bitcoin/bips/blob/master/bip-0032.mediawiki">BIP-32</see>.
/// </summary>
public static partial class Bip32HDWallet
{
	/// <summary>
	/// A BIP-32 extended key.
	/// </summary>
	public class ExtKey
	{
		private readonly Key key;
		private readonly FixedArrays fixedArrays;
		private readonly byte depth;
		private readonly bool isPrivate = true;
		private readonly bool testNet;
		private readonly uint childNumber;

		private ExtKey(Key key, ReadOnlySpan<byte> chainCode, bool testNet = false)
			: this(key, chainCode, parentFingerprint: default, depth: 0, testNet)
		{
		}

		private ExtKey(Key key, ReadOnlySpan<byte> chainCode, ReadOnlySpan<byte> parentFingerprint, byte depth, bool testNet = false)
		{
			this.key = key;
			this.depth = depth;
			this.testNet = testNet;
			this.fixedArrays = new(chainCode, parentFingerprint);
		}

		private static ReadOnlySpan<byte> MainNetPublic => new byte[] { 0x04, 0x88, 0xB2, 0x1E };

		private static ReadOnlySpan<byte> MainNetPrivate => new byte[] { 0x04, 0x88, 0xAD, 0xE4 };

		private static ReadOnlySpan<byte> TestNetPublic => new byte[] { 0x04, 0x35, 0x87, 0xCF };

		private static ReadOnlySpan<byte> TestNetPrivate => new byte[] { 0x04, 0x35, 0x83, 0x94 };

		private ReadOnlySpan<byte> ParentFingerprint => new byte[4];

		private ReadOnlySpan<byte> Version
		{
			get
			{
				return (this.testNet, this.isPrivate) switch
				{
					(false, true) => MainNetPrivate,
					(false, false) => MainNetPublic,
					(true, true) => TestNetPrivate,
					(true, false) => TestNetPublic,
				};
			}
		}

		/// <summary>
		/// Creates an extended key based on a <see cref="Bip39Mnemonic"/>.
		/// </summary>
		/// <param name="mnemonic">The mnemonic phrase from which to generate the master key.</param>
		/// <returns>The extended key.</returns>
		public static ExtKey Create(Bip39Mnemonic mnemonic)
		{
			Requires.NotNull(mnemonic);

			Span<byte> hmac = stackalloc byte[512 / 8];
			HMACSHA512.HashData("Bitcoin seed"u8, mnemonic.Seed, hmac);
			ReadOnlySpan<byte> masterKey = hmac[..32];
			ReadOnlySpan<byte> chainCode = hmac[32..];
			return new(new Key(masterKey), chainCode);
		}

		/// <inheritdoc/>
		public override string ToString()
		{
			Span<byte> data = stackalloc byte[78];
			int bytesWritten = this.WriteBytes(data);
			Debug.Assert(bytesWritten == data.Length, $"We only wrote {bytesWritten} bytes into our {data.Length} byte buffer.");

			Span<char> encoded = stackalloc char[112];
			int length = Base58Check.Encode(data, encoded);

			return encoded[..length].ToString();
		}

		/// <summary>
		/// Serializes the extended key to a buffer.
		/// </summary>
		/// <param name="destination">The buffer to write to. It must be at least 78 bytes in length.</param>
		/// <returns>The number of bytes written to <paramref name="destination"/>. Always 78.</returns>
		private int WriteBytes(Span<byte> destination)
		{
			int bytesWritten = 0;

			bytesWritten += this.Version.CopyToRetLength(destination);
			destination[bytesWritten++] = this.depth;
			bytesWritten += this.ParentFingerprint.CopyToRetLength(destination[bytesWritten..]);
			bytesWritten += BitUtilities.WriteLE(this.childNumber, destination[bytesWritten..]);
			bytesWritten += this.fixedArrays.ChainCode.CopyToRetLength(destination[bytesWritten..]);
			destination[bytesWritten++] = 0;
			bytesWritten += this.key.KeyMaterial.CopyToRetLength(destination[bytesWritten..]);

			return bytesWritten;
		}

		private unsafe struct FixedArrays
		{
			private const int ChainCodeLength = 32;
			private const int ParentFingerprintLength = 4;
			private fixed byte chainCode[ChainCodeLength];
			private fixed byte parentFingerprint[ParentFingerprintLength];

			internal FixedArrays(ReadOnlySpan<byte> chainCode, ReadOnlySpan<byte> parentFingerprint)
			{
				Requires.Argument(chainCode.Length == ChainCodeLength, nameof(chainCode), null);
				Requires.Argument(parentFingerprint.Length is 0 or ParentFingerprintLength, nameof(parentFingerprint), null);

				chainCode.CopyTo(this.ChainCodeWritable);
				parentFingerprint.CopyTo(this.ParentFingerprintWritable);
			}

			internal readonly ReadOnlySpan<byte> ChainCode => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(this.chainCode[0]), ChainCodeLength);

			internal readonly ReadOnlySpan<byte> ParentFingerprint => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(this.parentFingerprint[0]), ParentFingerprintLength);

			private Span<byte> ChainCodeWritable => MemoryMarshal.CreateSpan(ref this.chainCode[0], ChainCodeLength);

			private Span<byte> ParentFingerprintWritable => MemoryMarshal.CreateSpan(ref this.parentFingerprint[0], ParentFingerprintLength);
		}
	}

	private class Key
	{
		private readonly FixedArrays fixedArrays;

		internal Key(ReadOnlySpan<byte> keyMaterial)
		{
			this.fixedArrays = new(keyMaterial);
		}

		internal ReadOnlySpan<byte> KeyMaterial => this.fixedArrays.KeyMaterial;

		private unsafe struct FixedArrays
		{
			private const int KeyMaterialLength = 32;
			private fixed byte keyMaterial[KeyMaterialLength];

			internal FixedArrays(ReadOnlySpan<byte> key)
			{
				Requires.Argument(key.Length == KeyMaterialLength, nameof(key), null);
				key.CopyTo(this.KeyMaterialWritable);
			}

			internal readonly ReadOnlySpan<byte> KeyMaterial => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(this.keyMaterial[0]), KeyMaterialLength);

			private Span<byte> KeyMaterialWritable => MemoryMarshal.CreateSpan(ref this.keyMaterial[0], KeyMaterialLength);
		}
	}
}
