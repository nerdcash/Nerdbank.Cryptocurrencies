// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using NBitcoin.Secp256k1;

namespace Nerdbank.Bitcoin;

/// <summary>
/// An EC private key.
/// </summary>
[DebuggerDisplay($"{{{nameof(DebuggerDisplay)},nq}}")]
public class PrivateKey : IDisposable, IKey, IKeyWithTextEncoding
{
	/// <summary>
	/// Initializes a new instance of the <see cref="PrivateKey"/> class.
	/// </summary>
	/// <param name="key">The underlying cryptographic key.</param>
	/// <param name="isTestNet">A value indicating whether this key operates on the testnet.</param>
	public PrivateKey(ECPrivKey key, bool isTestNet)
	{
		this.CryptographicKey = key;
		this.PublicKey = new(this.CryptographicKey.CreatePubKey(), isTestNet);
	}

	/// <inheritdoc/>
	public bool IsTestNet => this.PublicKey.IsTestNet;

	/// <inheritdoc/>
	public string TextEncoding
	{
		get
		{
			// https://developer.bitcoin.org/devguide/wallets.html#wallet-import-format-wif
			Span<byte> keyMaterial = stackalloc byte[32];
			this.CryptographicKey.sec.WriteToSpan(keyMaterial);
			Span<byte> versionAndPayload = stackalloc byte[1 + keyMaterial.Length];
			versionAndPayload[0] = this.IsTestNet ? (byte)0xef : (byte)0x80;
			keyMaterial.CopyTo(versionAndPayload[1..]);
			Span<char> encoding = stackalloc char[Base58Check.GetMaxEncodedLength(versionAndPayload.Length)];
			int length = Base58Check.Encode(versionAndPayload, encoding);
			return encoding[..length].ToString();
		}
	}

	/// <summary>
	/// Gets the underlying cryptographic key.
	/// </summary>
	public ECPrivKey CryptographicKey { get; }

	/// <summary>
	/// Gets the public key counterpart to this private key.
	/// </summary>
	public PublicKey PublicKey { get; }

	/// <summary>
	/// Gets the Bitcoin address for this private key.
	/// </summary>
	public BitcoinP2PKHAddress P2PKHAddress => this.PublicKey.P2PKHAddress;

	private string DebuggerDisplay => $"{this.TextEncoding} ({this.PublicKey.P2PKHAddress})";

	/// <inheritdoc cref="IKeyWithTextEncoding.TryDecode(string, out DecodeError?, out string?, out IKeyWithTextEncoding?)"/>
	static bool IKeyWithTextEncoding.TryDecode(string encoding, [NotNullWhen(false)] out DecodeError? decodeError, [NotNullWhen(false)] out string? errorMessage, [NotNullWhen(true)] out IKeyWithTextEncoding? key)
	{
		if (TryDecode(encoding, out decodeError, out errorMessage, out PrivateKey? fvk))
		{
			key = fvk;
			return true;
		}

		key = null;
		return false;
	}

	/// <inheritdoc cref="IKeyWithTextEncoding.TryDecode(string, out DecodeError?, out string?, out IKeyWithTextEncoding?)"/>
	public static bool TryDecode(string encoding, [NotNullWhen(false)] out DecodeError? decodeError, [NotNullWhen(false)] out string? errorMessage, [NotNullWhen(true)] out PrivateKey? key)
	{
		Requires.NotNull(encoding);

		Span<byte> versionAndPayload = stackalloc byte[Base58Check.GetMaxDecodedLength(encoding.Length)];
		if (!Base58Check.TryDecode(encoding, versionAndPayload, out decodeError, out errorMessage, out int bytesWritten))
		{
			key = null;
			return false;
		}

		bool isTestNet;
		switch (versionAndPayload[0])
		{
			case 0x80:
				isTestNet = false;
				break;
			case 0xef:
				isTestNet = true;
				break;
			default:
				decodeError = DecodeError.UnrecognizedVersion;
				errorMessage = $"Invalid prefix: {versionAndPayload[0]:x2}";
				key = null;
				return false;
		}

		if (!ECPrivKey.TryCreate(versionAndPayload[1..], out ECPrivKey? privKey))
		{
			decodeError = DecodeError.InvalidKey;
			errorMessage = "Invalid private key material";
			key = null;
			return false;
		}

		key = new(privKey, isTestNet);
		return true;
	}

	/// <inheritdoc/>
	public void Dispose() => this.CryptographicKey.Dispose();
}
