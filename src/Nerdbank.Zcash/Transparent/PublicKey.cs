// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using NBitcoin.Secp256k1;
using Nerdbank.Zcash.FixedLengthStructs;

namespace Nerdbank.Zcash.Transparent;

/// <summary>
/// A public key used for transparent addresses.
/// </summary>
public class PublicKey : IZcashKey, IFullViewingKey
{
	private readonly Bytes33 keyMaterial;
	private readonly int keyLength;

	/// <summary>
	/// Initializes a new instance of the <see cref="PublicKey"/> class.
	/// </summary>
	/// <param name="publicKey">The cryptographic public key.</param>
	/// <param name="network">The zcash network this key will be used on.</param>
	public PublicKey(ReadOnlySpan<byte> publicKey, ZcashNetwork network)
	{
		this.keyMaterial = new(publicKey, allowShorterInput: true);
		this.keyLength = publicKey.Length;
		this.Network = network;

		this.DefaultAddress = new TransparentP2PKHAddress(new TransparentP2PKHReceiver(this), this.Network);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="PublicKey"/> class.
	/// </summary>
	/// <param name="publicKey">The cryptographic public key.</param>
	/// <param name="network">The zcash network this key will be used on.</param>
	public PublicKey(ECPubKey publicKey, ZcashNetwork network)
	{
		Requires.NotNull(publicKey);

		Span<byte> keyMaterial = stackalloc byte[33];
		publicKey.WriteToSpan(compressed: true, keyMaterial, out int bytesWritten);
		this.keyMaterial = new(keyMaterial, allowShorterInput: true);

		this.DefaultAddress = new TransparentP2PKHAddress(new TransparentP2PKHReceiver(this), this.Network);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="PublicKey"/> class.
	/// </summary>
	/// <param name="publicKey">The bitcoin public key that will be used for Zcash.</param>
	public PublicKey(Bitcoin.PublicKey publicKey)
	{
		Requires.NotNull(publicKey);

		Span<byte> keyMaterial = stackalloc byte[33];
		publicKey.CryptographicKey.WriteToSpan(compressed: true, keyMaterial, out this.keyLength);
		this.keyMaterial = new(keyMaterial, allowShorterInput: true);

		this.Network = publicKey.IsTestNet ? ZcashNetwork.TestNet : ZcashNetwork.MainNet;

		this.DefaultAddress = new TransparentP2PKHAddress(new TransparentP2PKHReceiver(this), this.Network);
	}

	/// <summary>
	/// Gets the public key.
	/// </summary>
	public ReadOnlySpan<byte> KeyMaterial => this.keyMaterial.Value[..this.keyLength];

	/// <inheritdoc/>
	public ZcashNetwork Network { get; }

	/// <inheritdoc/>
	public ZcashAddress DefaultAddress { get; }

	/// <inheritdoc/>
	public IIncomingViewingKey IncomingViewingKey => this;
}
