// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using NBitcoin.Secp256k1;

namespace Nerdbank.Zcash.Transparent;

/// <summary>
/// A public key used for transparent addresses.
/// </summary>
public class PublicKey : IZcashKey, IFullViewingKey
{
	private readonly PublicKeyMaterial keyMaterial;
	private readonly int keyLength;

	/// <summary>
	/// Initializes a new instance of the <see cref="PublicKey"/> class.
	/// </summary>
	/// <param name="publicKey">The cryptographic public key.</param>
	/// <param name="network">The zcash network this key will be used on.</param>
	public PublicKey(ReadOnlySpan<byte> publicKey, ZcashNetwork network)
	{
		publicKey.CopyTo(this.keyMaterial);
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

		publicKey.WriteToSpan(compressed: true, this.keyMaterial, out int bytesWritten);

		this.DefaultAddress = new TransparentP2PKHAddress(new TransparentP2PKHReceiver(this), this.Network);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="PublicKey"/> class.
	/// </summary>
	/// <param name="publicKey">The bitcoin public key that will be used for Zcash.</param>
	public PublicKey(Bitcoin.PublicKey publicKey)
	{
		Requires.NotNull(publicKey);

		publicKey.CryptographicKey.WriteToSpan(compressed: true, this.keyMaterial, out this.keyLength);
		this.Network = publicKey.IsTestNet ? ZcashNetwork.TestNet : ZcashNetwork.MainNet;
		this.DefaultAddress = new TransparentP2PKHAddress(new TransparentP2PKHReceiver(this), this.Network);
	}

	/// <summary>
	/// Gets the public key.
	/// </summary>
	public ReadOnlySpan<byte> KeyMaterial => this.keyMaterial[..this.keyLength];

	/// <inheritdoc/>
	public ZcashNetwork Network { get; }

	/// <inheritdoc/>
	public ZcashAddress DefaultAddress { get; }

	/// <inheritdoc/>
	public IIncomingViewingKey IncomingViewingKey => this;

	[InlineArray(Length)]
	private struct PublicKeyMaterial : IEquatable<PublicKeyMaterial>
	{
		public const int Length = 33;
		private byte element;

		/// <inheritdoc />
		readonly bool IEquatable<PublicKeyMaterial>.Equals(PublicKeyMaterial other) => this[..].SequenceEqual(other);

		/// <inheritdoc cref="IEquatable{T}.Equals(T)"/>
		public readonly bool Equals(in PublicKeyMaterial other) => this[..].SequenceEqual(other);
	}
}
