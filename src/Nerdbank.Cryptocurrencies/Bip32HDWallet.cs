// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Org.BouncyCastle.Crypto.Digests;
using Secp = NBitcoin.Secp256k1;

namespace Nerdbank.Cryptocurrencies;

/// <summary>
/// Implements Hierarchical Deterministic Wallets as defined in
/// <see href="https://github.com/bitcoin/bips/blob/master/bip-0032.mediawiki">BIP-32</see>.
/// </summary>
public static partial class Bip32HDWallet
{
	private const int PublicKeyLength = 33;
	private const int ChainCodeLength = 32;
	private const int ParentFingerprintLength = 4;

	/// <summary>
	/// A BIP-32 extended key.
	/// </summary>
	/// <seealso cref="ExtendedPrivateKey"/>
	/// <seealso cref="ExtendedPublicKey"/>
	public abstract class ExtendedKeyBase
	{
		private readonly FixedArrays fixedArrays;

		/// <summary>
		/// Initializes a new instance of the <see cref="ExtendedKeyBase"/> class
		/// for a master key.
		/// </summary>
		/// <param name="chainCode">The chain code.</param>
		/// <param name="testNet"><see langword="true" /> if this key is for use on a testnet; <see langword="false" /> otherwise.</param>
		internal ExtendedKeyBase(ReadOnlySpan<byte> chainCode, bool testNet = false)
		{
			this.fixedArrays = new(chainCode, default);
			this.IsTestNet = testNet;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ExtendedKeyBase"/> class
		/// for a derived key.
		/// </summary>
		/// <param name="parentKey">The key from which this one was derived.</param>
		/// <param name="childNumber">The index used when deriving this key.</param>
		internal ExtendedKeyBase(ExtendedKeyBase parentKey, uint childNumber)
			: this(parentKey.ChainCode, parentKey.Identifier[..4], checked((byte)(parentKey.Depth + 1)), childNumber, parentKey.IsTestNet)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ExtendedKeyBase"/> class
		/// for a derived key whose parent key is not available.
		/// </summary>
		/// <param name="chainCode"><inheritdoc cref="ExtendedKeyBase(ReadOnlySpan{byte}, bool)" path="/param[@name='chainCode']"/></param>
		/// <param name="parentFingerprint">The first four bytes of the parent key's <see cref="Identifier"/>.</param>
		/// <param name="depth">The depth of the parent key, plus 1.</param>
		/// <param name="childNumber">The index used when deriving this key.</param>
		/// <param name="testNet"><inheritdoc cref="ExtendedKeyBase(ReadOnlySpan{byte}, bool)" path="/param[@name='testNet']"/></param>
		internal ExtendedKeyBase(ReadOnlySpan<byte> chainCode, ReadOnlySpan<byte> parentFingerprint, byte depth, uint childNumber, bool testNet = false)
		{
			this.fixedArrays = new(chainCode, parentFingerprint);
			this.Depth = depth;
			this.ChildNumber = childNumber;
			this.IsTestNet = testNet;
		}

		/// <summary>
		/// Gets a value indicating whether this key belongs to a TestNet (as opposed to a MainNet).
		/// </summary>
		public bool IsTestNet { get; }

		/// <summary>
		/// Gets the identifier for this key.
		/// </summary>
		/// <remarks>
		/// Extended keys can be identified by the Hash160 (RIPEMD160 after SHA256) of the serialized ECDSA public key K, ignoring the chain code.
		/// This corresponds exactly to the data used in traditional Bitcoin addresses.
		/// It is not advised to represent this data in base58 format though, as it may be interpreted as an address that way
		/// (and wallet software is not required to accept payment to the chain key itself).
		/// </remarks>
		public abstract ReadOnlySpan<byte> Identifier { get; }

		/// <summary>
		/// Gets the number of derivations from the master key to this one.
		/// </summary>
		protected byte Depth { get; }

		/// <summary>
		/// Gets the index number used when deriving this key from its direct parent.
		/// </summary>
		protected uint ChildNumber { get; }

		/// <summary>
		/// Gets the first 32-bits of the <see cref="Identifier"/> of the parent key.
		/// </summary>
		protected ReadOnlySpan<byte> ParentFingerprint => this.fixedArrays.ParentFingerprint;

		/// <summary>
		/// Gets the chain code for this key.
		/// </summary>
		protected ReadOnlySpan<byte> ChainCode => this.fixedArrays.ChainCode;

		/// <summary>
		/// Gets a 4-byte version used in the binary representation of this extended key.
		/// </summary>
		protected abstract ReadOnlySpan<byte> Version { get; }

		/// <inheritdoc cref="TryParse(ReadOnlySpan{char}, out ExtendedKeyBase?, out DecodeError?, out string?)"/>
		/// <returns>The decoded extended key. An instance of either <see cref="ExtendedPrivateKey"/> or <see cref="ExtendedPublicKey"/>.</returns>
		/// <exception cref="FormatException">Thrown if parsing fails.</exception>
		public static ExtendedKeyBase Parse(ReadOnlySpan<char> extendedKeyEncoding)
		{
			if (!TryParse(extendedKeyEncoding, out ExtendedKeyBase? result, out _, out string? errorMessage))
			{
				throw new FormatException(errorMessage);
			}

			return result;
		}

		/// <summary>
		/// Parses an extended key in its common base58 representation (e.g. "xprv..." or "xpub...") into an <see cref="ExtendedKeyBase"/> instance.
		/// </summary>
		/// <param name="extendedKeyEncoding">The base58 encoding of an extended private or public key.</param>
		/// <param name="result">Receives the decoded key, if successful. An instance of either <see cref="ExtendedPrivateKey"/> or <see cref="ExtendedPublicKey"/>.</param>
		/// <param name="decodeError">Receives the decoding error, if applicable.</param>
		/// <param name="errorMessage">Receives an error message explaining the parsing failure, if applicable.</param>
		/// <returns><see langword="true" /> if parsing was successful; <see langword="false" /> otherwise.</returns>
		public static bool TryParse(ReadOnlySpan<char> extendedKeyEncoding, [NotNullWhen(true)] out ExtendedKeyBase? result, [NotNullWhen(false)] out DecodeError? decodeError, [NotNullWhen(false)] out string? errorMessage)
		{
			result = null;
			int maxBytesLength = Base58Check.GetMaxDecodedLength(extendedKeyEncoding.Length);
			Span<byte> bytes = stackalloc byte[maxBytesLength];
			if (!Base58Check.TryDecode(extendedKeyEncoding, bytes, out decodeError, out errorMessage, out int bytesWritten))
			{
				return false;
			}

			if (bytesWritten != 78)
			{
				decodeError = DecodeError.UnexpectedLength;
				errorMessage = $"Expected 78 bytes after base58 decoding, but got {bytesWritten}.";
				return false;
			}

			bytes = bytes[..bytesWritten];
			ReadOnlySpan<byte> version = bytes[..4];
			byte depth = bytes[4];
			ReadOnlySpan<byte> parentFingerprint = bytes[5..9];
			uint childNumber = BitUtilities.ReadUInt32BE(bytes[9..13]);
			ReadOnlySpan<byte> chainCode = bytes.Slice(13, ChainCodeLength);
			ReadOnlySpan<byte> keyMaterial = bytes[^PublicKeyLength..];

			if (depth == 0)
			{
				if (childNumber != 0)
				{
					decodeError = DecodeError.InvalidDerivationData;
					errorMessage = $"The key claims to be a master key but has the non-zero child number {childNumber}.";
					return false;
				}

				if (!parentFingerprint.SequenceEqual("\0\0\0\0"u8))
				{
					decodeError = DecodeError.InvalidDerivationData;
					errorMessage = "The key claims to be a master key but has non-zero parent fingerprint.";
					return false;
				}
			}

			bool isTestNet;
			bool isPrivateKey;
			if (version.SequenceEqual(ExtendedPrivateKey.MainNet))
			{
				isTestNet = false;
				isPrivateKey = true;
			}
			else if (version.SequenceEqual(ExtendedPublicKey.MainNet))
			{
				isTestNet = false;
				isPrivateKey = false;
			}
			else if (version.SequenceEqual(ExtendedPublicKey.TestNet))
			{
				isTestNet = true;
				isPrivateKey = false;
			}
			else if (version.SequenceEqual(ExtendedPrivateKey.TestNet))
			{
				isTestNet = true;
				isPrivateKey = true;
			}
			else
			{
				decodeError = DecodeError.UnrecognizedVersion;
				errorMessage = $"Unrecognized version: {Convert.ToHexString(version)}";
				return false;
			}

			if (isPrivateKey)
			{
				if (keyMaterial[0] != 0)
				{
					decodeError = DecodeError.InvalidKey;
					errorMessage = "Expected private key but this may be a public key.";
					return false;
				}

				if (!Secp.ECPrivKey.TryCreate(keyMaterial[1..], out Secp.ECPrivKey? ecKey))
				{
					decodeError = DecodeError.InvalidKey;
					errorMessage = "Invalid private key.";
					return false;
				}

				result = new ExtendedPrivateKey(new PrivateKey(ecKey), chainCode, parentFingerprint, depth, childNumber, isTestNet);
			}
			else
			{
				if (!Secp.ECPubKey.TryCreate(keyMaterial, ctx: null, out _, out Secp.ECPubKey? ecKey))
				{
					decodeError = DecodeError.InvalidKey;
					errorMessage = "Invalid public key.";
					return false;
				}

				result = new ExtendedPublicKey(new PublicKey(ecKey), chainCode, parentFingerprint, depth, childNumber, isTestNet);
			}

			return true;
		}

		/// <summary>
		/// Writes out the binary representation of this key in the standard Base58Check encoding.
		/// </summary>
		/// <returns>The Base58Check encoding of this key.</returns>
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
		/// Writes the key material.
		/// </summary>
		/// <param name="destination">The buffer to write to.</param>
		/// <returns>The number of bytes written. This should always be 33.</returns>
		protected abstract int WriteKeyMaterial(Span<byte> destination);

		/// <summary>
		/// Serializes the extended key to a buffer.
		/// </summary>
		/// <param name="destination">The buffer to write to. It must be at least 78 bytes in length.</param>
		/// <returns>The number of bytes written to <paramref name="destination"/>. Always 78.</returns>
		protected int WriteBytes(Span<byte> destination)
		{
			int bytesWritten = 0;

			bytesWritten += this.Version.CopyToRetLength(destination);
			destination[bytesWritten++] = this.Depth;
			bytesWritten += this.ParentFingerprint.CopyToRetLength(destination[bytesWritten..]);
			bytesWritten += BitUtilities.WriteBE(this.ChildNumber, destination[bytesWritten..]);
			bytesWritten += this.fixedArrays.ChainCode.CopyToRetLength(destination[bytesWritten..]);
			bytesWritten += this.WriteKeyMaterial(destination[bytesWritten..]);

			return bytesWritten;
		}

		private unsafe struct FixedArrays
		{
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

	public class ExtendedPrivateKey : ExtendedKeyBase, IDisposable
	{
		private readonly PrivateKey key;

		internal ExtendedPrivateKey(PrivateKey key, ReadOnlySpan<byte> chainCode, bool testNet = false)
			: this(key, chainCode, parentFingerprint: default, depth: 0, childNumber: 0, testNet)
		{
		}

		internal ExtendedPrivateKey(PrivateKey key, ReadOnlySpan<byte> chainCode, ReadOnlySpan<byte> parentFingerprint, byte depth, uint childNumber, bool testNet = false)
			: base(chainCode, parentFingerprint, depth, childNumber, testNet)
		{
			this.key = key;
			this.PublicKey = new ExtendedPublicKey(this.key.CreatePublicKey(), this.ChainCode, this.ParentFingerprint, this.Depth, this.ChildNumber, this.IsTestNet);
		}

		/// <summary>
		/// Gets the public extended key counterpart to this private key.
		/// </summary>
		public ExtendedPublicKey PublicKey { get; }

		/// <inheritdoc/>
		public override ReadOnlySpan<byte> Identifier => this.PublicKey.Identifier;

		/// <inheritdoc/>
		protected override ReadOnlySpan<byte> Version => this.IsTestNet ? TestNet : MainNet;

		internal static ReadOnlySpan<byte> MainNet => new byte[] { 0x04, 0x88, 0xAD, 0xE4 };

		internal static ReadOnlySpan<byte> TestNet => new byte[] { 0x04, 0x35, 0x83, 0x94 };

		/// <summary>
		/// Creates an extended key based on a <see cref="Bip39Mnemonic"/>.
		/// </summary>
		/// <param name="mnemonic">The mnemonic phrase from which to generate the master key.</param>
		/// <returns>The extended key.</returns>
		public static ExtendedPrivateKey Create(Bip39Mnemonic mnemonic) => Create(Requires.NotNull(mnemonic).Seed);

		/// <summary>
		/// Creates an extended key based on a seed.
		/// </summary>
		/// <param name="seed">The seed from which to generate the master key.</param>
		/// <returns>The extended key.</returns>
		public static ExtendedPrivateKey Create(ReadOnlySpan<byte> seed)
		{
			Span<byte> hmac = stackalloc byte[512 / 8];
			HMACSHA512.HashData("Bitcoin seed"u8, seed, hmac);
			ReadOnlySpan<byte> masterKey = hmac[..32];
			ReadOnlySpan<byte> chainCode = hmac[32..];

			return new ExtendedPrivateKey(new PrivateKey(NBitcoin.Secp256k1.ECPrivKey.Create(masterKey)), chainCode);
		}

		/// <summary>
		/// Derives a new extended private key that is a direct child of this one.
		/// </summary>
		/// <param name="childNumber">The child key number to derive. This may include the <see cref="KeyPath.HardenedBit"/> to derive a hardened key.</param>
		/// <returns>A derived extended private key.</returns>
		public ExtendedPrivateKey Derive(uint childNumber)
		{
			Span<byte> hashInput = stackalloc byte[PublicKeyLength + sizeof(uint)];
			BitUtilities.WriteBE(childNumber, hashInput[PublicKeyLength..]);
			if ((childNumber & KeyPath.HardenedBit) != 0)
			{
				this.key.Key.WriteToSpan(hashInput[1..]);
			}
			else
			{
				this.PublicKey.Key.Key.WriteToSpan(true, hashInput, out _);
			}

			Span<byte> hashOutput = stackalloc byte[512 / 8];
			HMACSHA512.HashData(this.ChainCode, hashInput, hashOutput);
			Span<byte> childKeyAdd = hashOutput[..32];
			Span<byte> childChainCode = hashOutput[32..];

			// From the spec:
			// In case parse256(IL) ≥ n or ki = 0, the resulting key is invalid,
			// and one should proceed with the next value for i.
			// (Note: this has probability lower than 1 in 2^127.)
			//// TODO: add check here.

			PrivateKey pvk = new(this.key.Key.TweakAdd(childKeyAdd));
			byte childDepth = checked((byte)(this.Depth + 1));

			return new ExtendedPrivateKey(pvk, childChainCode, this.Identifier[..4], childDepth, childNumber, this.IsTestNet);
		}

		/// <summary>
		/// Derives a new extended private key by following the steps in the specified path.
		/// </summary>
		/// <param name="keyPath">The derivation path to follow to produce the new key.</param>
		/// <returns>A derived extended private key.</returns>
		public ExtendedPrivateKey Derive(KeyPath keyPath)
		{
			Requires.NotNull(keyPath);
			if (this.Depth > 0)
			{
				throw new NotSupportedException("Deriving with a key path from a non-rooted key is not yet supported.");
			}

			ExtendedPrivateKey result = this;
			foreach (uint index in keyPath)
			{
				result = result.Derive(index);
			}

			return result;
		}

		/// <inheritdoc/>
		public void Dispose() => this.key.Dispose();

		/// <inheritdoc/>
		protected override int WriteKeyMaterial(Span<byte> destination)
		{
			destination[0] = 0;
			this.key.Key.WriteToSpan(destination[1..]);
			return 33;
		}
	}

	public class ExtendedPublicKey : ExtendedKeyBase
	{
		private readonly PublicKey key;
		private readonly FixedArrays fixedArrays;

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

		public override ReadOnlySpan<byte> Identifier => this.fixedArrays.Identifier;

		internal PublicKey Key => this.key;

		/// <inheritdoc/>
		protected override ReadOnlySpan<byte> Version => this.IsTestNet ? TestNet : MainNet;

		internal static ReadOnlySpan<byte> MainNet => new byte[] { 0x04, 0x88, 0xB2, 0x1E };

		internal static ReadOnlySpan<byte> TestNet => new byte[] { 0x04, 0x35, 0x87, 0xCF };

		/// <summary>
		/// Derives a new extended public key that is a direct child of this one.
		/// </summary>
		/// <param name="childNumber">
		/// The child key number to derive. Must <em>not</em> contain the <see cref="KeyPath.HardenedBit"/>.
		/// To derive a hardened child, use the <see cref="ExtendedPrivateKey"/>.
		/// </param>
		/// <returns>A derived extended public key.</returns>
		/// <exception cref="NotSupportedException">Thrown if <paramref name="childNumber"/> contains the <see cref="KeyPath.HardenedBit"/>.</exception>
		public ExtendedPublicKey Derive(uint childNumber)
		{
			if ((childNumber & KeyPath.HardenedBit) != 0)
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
			//// TODO: add check here.

			PublicKey pbk = new(this.key.Key.AddTweak(childKeyAdd));
			byte childDepth = checked((byte)(this.Depth + 1));

			return new ExtendedPublicKey(pbk, childChainCode, this.Identifier[..4], childDepth, childNumber, this.IsTestNet);
		}

		/// <summary>
		/// Derives a new extended public key by following the steps in the specified path.
		/// </summary>
		/// <param name="keyPath">The derivation path to follow to produce the new key.</param>
		/// <returns>A derived extended public key.</returns>
		public ExtendedPublicKey Derive(KeyPath keyPath)
		{
			Requires.NotNull(keyPath);
			if (this.Depth > 0)
			{
				throw new NotSupportedException("Deriving with a key path from a non-rooted key is not yet supported.");
			}

			ExtendedPublicKey result = this;
			foreach (uint index in keyPath)
			{
				result = result.Derive(index);
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

	internal class PrivateKey : IDisposable
	{
		internal PrivateKey(NBitcoin.Secp256k1.ECPrivKey key)
		{
			this.Key = key;
		}

		internal NBitcoin.Secp256k1.ECPrivKey Key { get; }

		/// <inheritdoc/>
		public void Dispose() => this.Key.Dispose();

		internal PublicKey CreatePublicKey() => new(this.Key.CreatePubKey());
	}

	internal class PublicKey
	{
		internal PublicKey(NBitcoin.Secp256k1.ECPubKey key)
		{
			this.Key = key;
		}

		internal NBitcoin.Secp256k1.ECPubKey Key { get; }
	}
}
