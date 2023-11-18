// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Secp = NBitcoin.Secp256k1;

namespace Nerdbank.Bitcoin;

public static partial class Bip32HDWallet
{
	/// <summary>
	/// A BIP-32 extended key.
	/// </summary>
	/// <seealso cref="ExtendedPrivateKey"/>
	/// <seealso cref="ExtendedPublicKey"/>
	public abstract class ExtendedKeyBase : IExtendedKey, IKeyWithTextEncoding
	{
		private readonly ParentFingerprint parentFingerprint;
		private readonly ChainCode chainCode;

		/// <summary>
		/// Backing field for the <see cref="TextEncoding"/> property.
		/// </summary>
		private string? textEncoding;

		/// <summary>
		/// Initializes a new instance of the <see cref="ExtendedKeyBase"/> class
		/// for a derived key whose parent key is not available.
		/// </summary>
		/// <param name="chainCode">The chain code.</param>
		/// <param name="parentFingerprint">The first four bytes of the parent key's <see cref="Identifier"/>.</param>
		/// <param name="depth">The depth of the parent key, plus 1.</param>
		/// <param name="childIndex">The index used when deriving this key.</param>
		/// <param name="testNet"><see langword="true" /> if this key is for use on a testnet; <see langword="false" /> otherwise.</param>
		internal ExtendedKeyBase(in ChainCode chainCode, in ParentFingerprint parentFingerprint, byte depth, uint childIndex, bool testNet)
		{
			this.chainCode = chainCode;
			this.parentFingerprint = parentFingerprint;
			this.Depth = depth;
			this.ChildIndex = childIndex;
			this.IsTestNet = testNet;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ExtendedKeyBase"/> class.
		/// </summary>
		/// <param name="copyFrom">The key to copy from.</param>
		protected ExtendedKeyBase(ExtendedKeyBase copyFrom)
			: this(Requires.NotNull(copyFrom).ChainCode, copyFrom.ParentFingerprint, copyFrom.Depth, copyFrom.ChildIndex, copyFrom.IsTestNet)
		{
			this.DerivationPath = copyFrom.DerivationPath;
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
		public byte Depth { get; }

		/// <inheritdoc/>
		public uint ChildIndex { get; }

		/// <inheritdoc/>
		public string TextEncoding
		{
			get
			{
				if (this.textEncoding is null)
				{
					Span<byte> data = stackalloc byte[78];
					int bytesWritten = this.WriteBytes(data);
					Debug.Assert(bytesWritten == data.Length, $"We only wrote {bytesWritten} bytes into our {data.Length} byte buffer.");

					Span<char> encoded = stackalloc char[112];
					int length = Base58Check.Encode(data, encoded);

					this.textEncoding = encoded[..length].ToString();
				}

				return this.textEncoding;
			}
		}

		/// <inheritdoc/>
		public Bip32KeyPath? DerivationPath { get; init; }

		/// <summary>
		/// Gets the first 32-bits of the <see cref="Identifier"/> of the parent key.
		/// </summary>
		protected ref readonly ParentFingerprint ParentFingerprint => ref this.parentFingerprint;

		/// <summary>
		/// Gets the chain code for this key.
		/// </summary>
		protected ref readonly ChainCode ChainCode => ref this.chainCode;

		/// <summary>
		/// Gets a 4-byte version used in the binary representation of this extended key.
		/// </summary>
		protected abstract ReadOnlySpan<byte> Version { get; }

		/// <summary>
		/// Gets a suggested string for use in the <see cref="DebuggerDisplayAttribute"/>.
		/// </summary>
		protected string DebuggerDisplay => $"{this.GetType().Name} {this.DerivationPath}";

		/// <inheritdoc cref="TryDecode(ReadOnlySpan{char}, out DecodeError?, out string?, out ExtendedKeyBase?)"/>
		/// <returns>The decoded extended key. An instance of either <see cref="ExtendedPrivateKey"/> or <see cref="ExtendedPublicKey"/>.</returns>
		/// <exception cref="FormatException">Thrown if parsing fails.</exception>
		public static ExtendedKeyBase Decode(ReadOnlySpan<char> extendedKeyEncoding)
		{
			if (!TryDecode(extendedKeyEncoding, out _, out string? errorMessage, out ExtendedKeyBase? result))
			{
				throw new FormatException(errorMessage);
			}

			return result;
		}

		/// <inheritdoc cref="IKeyWithTextEncoding.TryDecode(string, out DecodeError?, out string?, out IKeyWithTextEncoding?)"/>
		static bool IKeyWithTextEncoding.TryDecode(string encoding, [NotNullWhen(false)] out DecodeError? decodeError, [NotNullWhen(false)] out string? errorMessage, [NotNullWhen(true)] out IKeyWithTextEncoding? key)
		{
			if (TryDecode(encoding, out decodeError, out errorMessage, out ExtendedKeyBase? kb))
			{
				key = kb;
				return true;
			}

			key = null;
			return false;
		}

		/// <summary>
		/// Parses an extended key in its common base58 representation (e.g. "xprv..." or "xpub...") into an <see cref="ExtendedKeyBase"/> instance.
		/// </summary>
		/// <param name="encoding">The base58 encoding of an extended private or public key.</param>
		/// <param name="decodeError">Receives the decoding error, if applicable.</param>
		/// <param name="errorMessage">Receives an error message explaining the parsing failure, if applicable.</param>
		/// <param name="result">Receives the decoded key, if successful. An instance of either <see cref="ExtendedPrivateKey"/> or <see cref="ExtendedPublicKey"/>.</param>
		/// <returns><see langword="true" /> if parsing was successful; <see langword="false" /> otherwise.</returns>
		public static bool TryDecode(ReadOnlySpan<char> encoding, [NotNullWhen(false)] out DecodeError? decodeError, [NotNullWhen(false)] out string? errorMessage, [NotNullWhen(true)] out ExtendedKeyBase? result)
		{
			result = null;
			int maxBytesLength = Base58Check.GetMaxDecodedLength(encoding.Length);
			Span<byte> bytes = stackalloc byte[maxBytesLength];
			if (!Base58Check.TryDecode(encoding, bytes, out decodeError, out errorMessage, out int bytesWritten))
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
			ref readonly ParentFingerprint parentFingerprint = ref ParentFingerprint.From(bytes[5..9]);
			uint childIndex = BitUtilities.ReadUInt32BE(bytes[9..13]);
			ref readonly ChainCode chainCode = ref ChainCode.From(bytes.Slice(13, ChainCode.Length));
			ReadOnlySpan<byte> keyMaterial = bytes[^PublicKeyLength..];

			if (depth == 0)
			{
				if (childIndex != 0)
				{
					decodeError = DecodeError.InvalidDerivationData;
					errorMessage = $"The key claims to be a master key but has the non-zero child number {childIndex}.";
					return false;
				}

				if (!parentFingerprint.Equals(default))
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

				result = new ExtendedPrivateKey(ecKey, chainCode, parentFingerprint, depth, childIndex, isTestNet);
			}
			else
			{
				if (!Secp.ECPubKey.TryCreate(keyMaterial, ctx: null, out _, out Secp.ECPubKey? ecKey))
				{
					decodeError = DecodeError.InvalidKey;
					errorMessage = "Invalid public key.";
					return false;
				}

				result = new ExtendedPublicKey(ecKey, chainCode, parentFingerprint, depth, childIndex, isTestNet);
			}

			return true;
		}

		/// <summary>
		/// Writes out the standard Base58Check encoding.
		/// </summary>
		/// <returns>The Base58Check encoding of this key.</returns>
		public override string ToString() => this.TextEncoding;

		/// <inheritdoc/>
		public abstract IExtendedKey Derive(uint childIndex);

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
			bytesWritten += this.ParentFingerprint[..].CopyToRetLength(destination[bytesWritten..]);
			bytesWritten += BitUtilities.WriteBE(this.ChildIndex, destination[bytesWritten..]);
			bytesWritten += this.ChainCode[..].CopyToRetLength(destination[bytesWritten..]);
			bytesWritten += this.WriteKeyMaterial(destination[bytesWritten..]);

			return bytesWritten;
		}
	}
}
