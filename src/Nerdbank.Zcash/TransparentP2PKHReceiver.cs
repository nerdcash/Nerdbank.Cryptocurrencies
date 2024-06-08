// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash;

/// <summary>
/// A receiver that contains the cryptography parameters required to send Zcash to the <see cref="Pool.Transparent"/> pool
/// by way of a Pay to Public Key Hash method.
/// </summary>
[InlineArray(Length)]
public struct TransparentP2PKHReceiver : IUnifiedPoolReceiver, IEquatable<TransparentP2PKHReceiver>
{
	/// <summary>
	/// Gets the length of the receiver, in bytes.
	/// </summary>
	public const int Length = 160 / 8;

	private byte validatingKeyHash;

	/// <summary>
	/// Initializes a new instance of the <see cref="TransparentP2PKHReceiver"/> struct.
	/// </summary>
	/// <param name="p2pkh">The validating key hash.</param>
	/// <exception cref="ArgumentException">Thrown when the arguments have an unexpected length.</exception>
	public TransparentP2PKHReceiver(ReadOnlySpan<byte> p2pkh)
	{
		if (p2pkh.Length != Length)
		{
			throw new ArgumentException($"Length must be exactly {Length}, but was {p2pkh.Length}.", nameof(p2pkh));
		}

		p2pkh.CopyTo(this);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="TransparentP2PKHReceiver"/> struct.
	/// </summary>
	/// <param name="publicKey">The EC public key to create a receiver for.</param>
	public TransparentP2PKHReceiver(Zip32HDWallet.Transparent.ExtendedViewingKey publicKey)
	{
		Requires.NotNull(publicKey);

		Span<byte> serializedPublicKey = stackalloc byte[33];
		publicKey.CryptographicKey.WriteToSpan(compressed: true, serializedPublicKey, out int length);
		serializedPublicKey = serializedPublicKey[..length];

		Assumes.True(Bitcoin.PublicKey.CreatePublicKeyHash(serializedPublicKey, this) == Length);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="TransparentP2PKHReceiver"/> struct.
	/// </summary>
	/// <param name="publicKey">The EC public key to create a receiver for.</param>
	public TransparentP2PKHReceiver(Transparent.PublicKey publicKey)
	{
		Requires.NotNull(publicKey);

		Assumes.True(Bitcoin.PublicKey.CreatePublicKeyHash(publicKey.KeyMaterial, this) == Length);
	}

	/// <inheritdoc cref="IUnifiedPoolReceiver.UnifiedReceiverTypeCode"/>
	public static byte UnifiedReceiverTypeCode => UnifiedTypeCodes.TransparentP2PKH;

	/// <inheritdoc/>
	public readonly Pool Pool => Pool.Transparent;

	/// <inheritdoc />
	readonly int IPoolReceiver.EncodingLength => Length;

	/// <inheritdoc/>
	public readonly int Encode(Span<byte> buffer) => this[..].CopyToRetLength(buffer);

	/// <inheritdoc/>
	readonly bool IEquatable<TransparentP2PKHReceiver>.Equals(TransparentP2PKHReceiver other) => this.Equals(other);

	/// <inheritdoc cref="IEquatable{T}.Equals(T)"/>
	public readonly bool Equals(in TransparentP2PKHReceiver other) => this[..].SequenceEqual(other[..]);

	/// <inheritdoc/>
	public override readonly bool Equals([NotNullWhen(true)] object? obj) => obj is TransparentP2PKHReceiver other && this.Equals(other);

	/// <inheritdoc/>
	public override readonly int GetHashCode()
	{
		HashCode hashCode = default;
		hashCode.AddBytes(this[..]);
		return hashCode.ToHashCode();
	}
}
