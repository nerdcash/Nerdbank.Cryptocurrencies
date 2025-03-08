// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using NBitcoin.Secp256k1;

namespace Nerdbank.Bitcoin;

/// <summary>
/// A P2PKH bitcoin address (for payments to public key hashes).
/// </summary>
[DebuggerDisplay($"{{{nameof(TextEncoding)},nq}}")]
public class BitcoinP2PKHAddress
{
	private readonly PublicKeyHashArray publicKeyHash;

	/// <summary>
	/// Initializes a new instance of the <see cref="BitcoinP2PKHAddress"/> class.
	/// </summary>
	/// <param name="pubKey">The public key behind the address.</param>
	/// <param name="isTestNet">A value indicating whether the key is used on the Bitcoin testnet (as opposed to the more typical mainnet).</param>
	public BitcoinP2PKHAddress(ECPubKey pubKey, bool isTestNet)
	{
		Requires.NotNull(pubKey);

		// https://www.freecodecamp.org/news/how-to-create-a-bitcoin-wallet-address-from-a-private-key-eca3ddd9c05f/
		Span<byte> pubKeyBytes = stackalloc byte[100];
		pubKey.WriteToSpan(true, pubKeyBytes, out int bytesWritten);
		pubKeyBytes = pubKeyBytes[..bytesWritten];

		Span<byte> payloadWithVersion = stackalloc byte[1 + 20];
		payloadWithVersion[0] = isTestNet ? (byte)0x6f : (byte)0x00;

		PublicKey.CreatePublicKeyHash(pubKeyBytes, payloadWithVersion[1..]);

		Span<char> address = stackalloc char[Base58Check.GetMaxEncodedLength(payloadWithVersion.Length)];
		int charLength = Base58Check.Encode(payloadWithVersion, address);

		this.TextEncoding = address[..charLength].ToString();
		this.IsTestNet = isTestNet;
		this.publicKeyHash = new(payloadWithVersion[1..]);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="BitcoinP2PKHAddress"/> class.
	/// </summary>
	/// <param name="address">The address.</param>
	/// <param name="isTestNet">A value indicating whether this address operates on the testnet.</param>
	/// <param name="publicKeyHash">The public key hash.</param>
	private BitcoinP2PKHAddress(string address, bool isTestNet, ReadOnlySpan<byte> publicKeyHash)
	{
		this.TextEncoding = address;
		this.IsTestNet = isTestNet;
		this.publicKeyHash = new(publicKeyHash);
	}

	/// <summary>
	/// Gets the address.
	/// </summary>
	public string TextEncoding { get; }

	/// <summary>
	/// Gets a value indicating whether this address operates on the testnet.
	/// </summary>
	public bool IsTestNet { get; }

	/// <summary>
	/// Gets the public key hash of this address.
	/// </summary>
	public ReadOnlySpan<byte> PublicKeyHash => this.publicKeyHash;

	/// <summary>
	/// Tries to decode a Bitcoin address.
	/// </summary>
	/// <param name="address">The address to decode.</param>
	/// <param name="decodeError">The error that occurred while decoding the address.</param>
	/// <param name="errorMessage">The error message that occurred while decoding the address.</param>
	/// <param name="bitcoinAddress">The decoded address.</param>
	/// <returns><see langword="true"/> if the address was decoded successfully; <see langword="false"/> otherwise.</returns>
	public static bool TryDecode(string address, [NotNullWhen(false)] out DecodeError? decodeError, [NotNullWhen(false)] out string? errorMessage, [NotNullWhen(true)] out BitcoinP2PKHAddress? bitcoinAddress)
	{
		Requires.NotNull(address);

		Span<byte> versionAndPayload = stackalloc byte[Base58Check.GetMaxEncodedLength(address.Length)];
		if (!Base58Check.TryDecode(address, versionAndPayload, out decodeError, out errorMessage, out int bytesWritten))
		{
			bitcoinAddress = null;
			return false;
		}

		if (bytesWritten != 21)
		{
			decodeError = DecodeError.UnexpectedLength;
			errorMessage = $"Unexpected length: {bytesWritten}";
			bitcoinAddress = null;
			return false;
		}

		versionAndPayload = versionAndPayload[..bytesWritten];
		byte versionByte = versionAndPayload[0];
		Span<byte> publicKeyHash = versionAndPayload[1..];

		bool isTestNet;
		switch (versionByte)
		{
			case 0x0:
				isTestNet = false;
				break;
			case 0x6f:
				isTestNet = true;
				break;
			default:
				decodeError = DecodeError.UnrecognizedVersion;
				errorMessage = $"Unrecognized version byte: {versionByte}";
				bitcoinAddress = null;
				return false;
		}

		bitcoinAddress = new(address, isTestNet, publicKeyHash);
		return true;
	}

	/// <inheritdoc/>
	public override string ToString() => this.TextEncoding;

	[InlineArray(Length)]
	private struct PublicKeyHashArray : IEquatable<PublicKeyHashArray>
	{
		public const int Length = 20;

		private byte element;

		internal PublicKeyHashArray(ReadOnlySpan<byte> value)
		{
			value.CopyToWithLengthCheck(this);
		}

		/// <summary>
		/// Returns a strongly-typed struct over a span of bytes without incuring the cost of a memory copy.
		/// </summary>
		/// <param name="value">The bytes containing the value. This should have a length equal to <see cref="Length"/>.</param>
		/// <returns>The strongly-typed element.</returns>
		public static ref readonly PublicKeyHashArray From(ReadOnlySpan<byte> value) => ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, PublicKeyHashArray>(value));

		/// <inheritdoc/>
		readonly bool IEquatable<PublicKeyHashArray>.Equals(PublicKeyHashArray other) => this[..].SequenceEqual(other);

		/// <inheritdoc cref="IEquatable{T}.Equals"/>
		public readonly bool Equals(in PublicKeyHashArray other) => this[..].SequenceEqual(other);
	}
}
