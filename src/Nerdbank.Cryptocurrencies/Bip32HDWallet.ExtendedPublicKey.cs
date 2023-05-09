// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Org.BouncyCastle.Crypto.Digests;

namespace Nerdbank.Cryptocurrencies;

public static partial class Bip32HDWallet
{
	/// <summary>
	/// A BIP-39 extended public key.
	/// </summary>
	public class ExtendedPublicKey : ExtendedKeyBase
	{
		private readonly PublicKey key;
		private readonly FixedArrays fixedArrays;

		/// <summary>
		/// Initializes a new instance of the <see cref="ExtendedPublicKey"/> class.
		/// </summary>
		/// <param name="key">The public key.</param>
		/// <param name="chainCode"><inheritdoc cref="ExtendedKeyBase(ReadOnlySpan{byte}, ReadOnlySpan{byte}, byte, uint, bool)" path="/param[@name='chainCode']"/></param>
		/// <param name="parentFingerprint"><inheritdoc cref="ExtendedKeyBase(ReadOnlySpan{byte}, ReadOnlySpan{byte}, byte, uint, bool)" path="/param[@name='parentFingerprint']"/></param>
		/// <param name="depth"><inheritdoc cref="ExtendedKeyBase(ReadOnlySpan{byte}, ReadOnlySpan{byte}, byte, uint, bool)" path="/param[@name='depth']"/></param>
		/// <param name="childNumber"><inheritdoc cref="ExtendedKeyBase(ReadOnlySpan{byte}, ReadOnlySpan{byte}, byte, uint, bool)" path="/param[@name='childNumber']"/></param>
		/// <param name="testNet"><inheritdoc cref="ExtendedKeyBase(ReadOnlySpan{byte}, ReadOnlySpan{byte}, byte, uint, bool)" path="/param[@name='testNet']"/></param>
		internal ExtendedPublicKey(PublicKey key, ReadOnlySpan<byte> chainCode, ReadOnlySpan<byte> parentFingerprint, byte depth, uint childNumber, bool testNet = false)
			: base(chainCode, parentFingerprint, depth, childNumber, testNet)
		{
			this.key = key;

			Span<byte> publicKey = stackalloc byte[33];
			key.Key.WriteToSpan(true, publicKey, out int publicKeyLength);

			Span<byte> sha256Hash = stackalloc byte[256 / 8];
			int firstHashLength = SHA256.HashData(publicKey[..publicKeyLength], sha256Hash);

			RipeMD160Digest digest160 = new();
			Span<byte> identifier = stackalloc byte[digest160.GetDigestSize()];
			digest160.BlockUpdate(sha256Hash[..firstHashLength]);
			int finalHashLength = digest160.DoFinal(identifier);

			this.fixedArrays = new(identifier[..finalHashLength]);
		}

		/// <inheritdoc/>
		public override ReadOnlySpan<byte> Identifier => this.fixedArrays.Identifier;

		/// <summary>
		/// Gets the key.
		/// </summary>
		public PublicKey Key => this.key;

		/// <summary>
		/// Gets the version header for public keys on mainnet.
		/// </summary>
		internal static ReadOnlySpan<byte> MainNet => new byte[] { 0x04, 0x88, 0xB2, 0x1E };

		/// <summary>
		/// Gets the version header for public keys on testnet.
		/// </summary>
		internal static ReadOnlySpan<byte> TestNet => new byte[] { 0x04, 0x35, 0x87, 0xCF };

		/// <inheritdoc/>
		protected override ReadOnlySpan<byte> Version => this.IsTestNet ? TestNet : MainNet;

		/// <summary>
		/// Derives a new extended public key that is a direct child of this one.
		/// </summary>
		/// <param name="childNumber">
		/// The child key number to derive. Must <em>not</em> contain the <see cref="HardenedBit"/>.
		/// To derive a hardened child, use the <see cref="ExtendedPrivateKey"/>.
		/// </param>
		/// <returns>A derived extended public key.</returns>
		/// <exception cref="NotSupportedException">Thrown if <paramref name="childNumber"/> contains the <see cref="HardenedBit"/>.</exception>
		/// <exception cref="InvalidKeyException">
		/// Thrown in a statistically extremely unlikely event of the derived key being invalid.
		/// Callers should handle this exception by requesting a new key with an incremented value
		/// for <paramref name="childNumber"/>.
		/// </exception>
		public ExtendedPublicKey Derive(uint childNumber)
		{
			if ((childNumber & HardenedBit) != 0)
			{
				throw new NotSupportedException(Strings.CannotDeriveHardenedChildFromPublicKey);
			}

			Span<byte> hashInput = stackalloc byte[PublicKeyLength + sizeof(uint)];
			this.Key.Key.WriteToSpan(true, hashInput, out _);
			BitUtilities.WriteBE(childNumber, hashInput[PublicKeyLength..]);

			Span<byte> hashOutput = stackalloc byte[512 / 8];
			HMACSHA512.HashData(this.ChainCode, hashInput, hashOutput);
			Span<byte> childKeyAdd = hashOutput[..32];
			Span<byte> childChainCode = hashOutput[32..];

			// From the spec:
			// In case parse256(IL) ≥ n or Ki is the point at infinity, the resulting key is invalid,
			// and one should proceed with the next value for i.
			if (!this.key.Key.TryAddTweak(childKeyAdd, out NBitcoin.Secp256k1.ECPubKey? pubKey))
			{
				throw new InvalidKeyException(Strings.VeryUnlikelyInvalidChildKey);
			}

			byte childDepth = checked((byte)(this.Depth + 1));

			return new ExtendedPublicKey(new(pubKey), childChainCode, this.Identifier[..4], childDepth, childNumber, this.IsTestNet);
		}

		/// <summary>
		/// Derives a new extended public key by following the steps in the specified path.
		/// </summary>
		/// <param name="keyPath">The derivation path to follow to produce the new key.</param>
		/// <returns>A derived extended public key.</returns>
		/// <exception cref="InvalidKeyException">
		/// Thrown in a statistically extremely unlikely event of the derived key being invalid.
		/// Callers should handle this exception by requesting a new key with an incremented value
		/// for the child number at the failing position in the key path.
		/// </exception>
		public ExtendedPublicKey Derive(KeyPath keyPath)
		{
			Requires.NotNull(keyPath);

			if (this.Depth > 0 && keyPath.IsRooted)
			{
				throw new NotSupportedException("Deriving with a rooted key path from a non-rooted key is not supported.");
			}

			ExtendedPublicKey result = this;
			foreach (KeyPath step in keyPath.Steps)
			{
				try
				{
					result = result.Derive(step.Index);
				}
				catch (InvalidKeyException ex)
				{
					throw new InvalidKeyException(Strings.FormatVeryUnlikelyUnvalidChildKeyOnPath(step), ex) { KeyPath = step };
				}
			}

			return result;
		}

		/// <inheritdoc/>
		protected override int WriteKeyMaterial(Span<byte> destination)
		{
			this.key.Key.WriteToSpan(compressed: true, destination, out int written);
			return written;
		}

		private unsafe struct FixedArrays
		{
			internal const int IdentifierLength = 160 / 8;
			private fixed byte identifier[IdentifierLength];

			internal FixedArrays(ReadOnlySpan<byte> identifier)
			{
				Requires.Argument(identifier.Length == IdentifierLength, nameof(identifier), "Unexpected length.");
				identifier.CopyTo(this.IdentifierWritable);
			}

			internal readonly ReadOnlySpan<byte> Identifier => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(this.identifier[0]), IdentifierLength);

			private Span<byte> IdentifierWritable => MemoryMarshal.CreateSpan(ref this.identifier[0], IdentifierLength);
		}
	}
}
