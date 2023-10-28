// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using NBitcoin.Secp256k1;
using Nerdbank.Cryptocurrencies;
using Nerdbank.Zcash.FixedLengthStructs;

namespace Nerdbank.Zcash.Transparent;

/// <summary>
/// A private key used for transparent addresses.
/// </summary>
public class PrivateKey : ISpendingKey, IKeyWithTextEncoding, IZcashKey
{
	private readonly Bytes32 privateKey;

	/// <summary>
	/// Initializes a new instance of the <see cref="PrivateKey"/> class.
	/// </summary>
	/// <param name="key">The cryptographic private key.</param>
	/// <param name="network">The Zcash network this key will operate on.</param>
	public PrivateKey(ECPrivKey key, ZcashNetwork network)
	{
		Requires.NotNull(key);

		Span<byte> keyMaterial = stackalloc byte[32];
		key.sec.WriteToSpan(keyMaterial);
		this.privateKey = new(keyMaterial);

		ECPubKey publicKey = key.CreatePubKey();
		this.PublicKey = new(publicKey, network);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="PrivateKey"/> class.
	/// </summary>
	/// <param name="privateKey">The Bitcoin private key that will be used for Zcash.</param>
	public PrivateKey(Bitcoin.PrivateKey privateKey)
	{
		Requires.NotNull(privateKey);

		Span<byte> keyMaterial = stackalloc byte[32];
		privateKey.CryptographicKey.sec.WriteToSpan(keyMaterial);
		this.privateKey = new(keyMaterial);

		this.PublicKey = new PublicKey(privateKey.PublicKey);
	}

	/// <summary>
	/// Gets the private key data.
	/// </summary>
	public ReadOnlySpan<byte> KeyMaterial => this.privateKey.Value;

	/// <summary>
	/// Gets the Zcash network this key is used on.
	/// </summary>
	public ZcashNetwork Network => this.PublicKey.Network;

	/// <summary>
	/// Gets the public key.
	/// </summary>
	public PublicKey PublicKey { get; }

	/// <inheritdoc/>
	IFullViewingKey ISpendingKey.FullViewingKey => this.PublicKey;

	/// <inheritdoc/>
	public string TextEncoding => Bitcoin.PrivateKey.Encode(this.KeyMaterial, this.Network.IsTestNet());

	/// <inheritdoc cref="IKeyWithTextEncoding.TryDecode(string, out DecodeError?, out string?, out IKeyWithTextEncoding?)"/>
	public static bool TryDecode(string encoding, [NotNullWhen(false)] out DecodeError? decodeError, [NotNullWhen(false)] out string? errorMessage, [NotNullWhen(true)] out PrivateKey? key)
	{
		if (Bitcoin.PrivateKey.TryDecode(encoding, out decodeError, out errorMessage, out Bitcoin.PrivateKey? bitcoinPrivateKey))
		{
			key = new(bitcoinPrivateKey);
			return true;
		}

		key = null;
		return false;
	}

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
}
