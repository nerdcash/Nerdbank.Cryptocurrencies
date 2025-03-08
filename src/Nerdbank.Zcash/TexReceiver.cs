// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash;

/// <summary>
/// A receiver that contains the cryptography parameters required to send Zcash to the <see cref="Pool.Transparent"/> pool
/// by way of a Pay to Public Key Hash method.
/// </summary>
/// <remarks>
/// This receiver is used for <see cref="TexAddress"/> to represent receivers that must only be sent transparent funds.
/// It is otherwise equivalent to <see cref="TransparentP2PKHReceiver"/>.
/// </remarks>
[InlineArray(Length)]
public struct TexReceiver : IPoolReceiver, IEquatable<TexReceiver>
{
	/// <summary>
	/// Gets the length of the receiver, in bytes.
	/// </summary>
	public const int Length = 160 / 8;

	private byte validatingKeyHash;

	/// <summary>
	/// Initializes a new instance of the <see cref="TexReceiver"/> struct.
	/// </summary>
	/// <param name="p2pkh">The validating key hash.</param>
	/// <exception cref="ArgumentException">Thrown when the arguments have an unexpected length.</exception>
	public TexReceiver(ReadOnlySpan<byte> p2pkh)
	{
		if (p2pkh.Length != Length)
		{
			throw new ArgumentException($"Length must be exactly {Length}, but was {p2pkh.Length}.", nameof(p2pkh));
		}

		p2pkh.CopyTo(this);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="TexReceiver"/> struct.
	/// </summary>
	/// <param name="publicKey">The EC public key to create a receiver for.</param>
	public TexReceiver(Zip32HDWallet.Transparent.ExtendedViewingKey publicKey)
	{
		Requires.NotNull(publicKey);

		Span<byte> serializedPublicKey = stackalloc byte[33];
		publicKey.CryptographicKey.WriteToSpan(compressed: true, serializedPublicKey, out int length);
		serializedPublicKey = serializedPublicKey[..length];

		Assumes.True(Bitcoin.PublicKey.CreatePublicKeyHash(serializedPublicKey, this) == Length);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="TexReceiver"/> struct.
	/// </summary>
	/// <param name="publicKey">The EC public key to create a receiver for.</param>
	public TexReceiver(Transparent.PublicKey publicKey)
	{
		Requires.NotNull(publicKey);

		Assumes.True(Bitcoin.PublicKey.CreatePublicKeyHash(publicKey.KeyMaterial, this) == Length);
	}

	/// <inheritdoc/>
	public readonly Pool Pool => Pool.Transparent;

	/// <inheritdoc />
	readonly int IPoolReceiver.EncodingLength => Length;

	/// <summary>
	/// Converts a <see cref="TransparentP2PKHReceiver"/> into a <see cref="TexReceiver" />.
	/// </summary>
	/// <param name="receiver">The receiver to convert from.</param>
	public static implicit operator TexReceiver(in TransparentP2PKHReceiver receiver) => new(receiver);

	/// <summary>
	/// Converts a <see cref="TexReceiver"/> into a <see cref="TransparentP2PKHReceiver" />.
	/// </summary>
	/// <param name="receiver">The receiver to convert from.</param>
	public static explicit operator TransparentP2PKHReceiver(in TexReceiver receiver) => new(receiver);

	/// <inheritdoc/>
	public readonly int Encode(Span<byte> buffer) => this[..].CopyToRetLength(buffer);

	/// <inheritdoc/>
	readonly bool IEquatable<TexReceiver>.Equals(TexReceiver other) => this.Equals(other);

	/// <inheritdoc cref="IEquatable{T}.Equals(T)"/>
	public readonly bool Equals(in TexReceiver other) => this[..].SequenceEqual(other[..]);

	/// <inheritdoc/>
	public override readonly bool Equals([NotNullWhen(true)] object? obj) => obj is TexReceiver other && this.Equals(other);

	/// <inheritdoc/>
	public override readonly int GetHashCode()
	{
		HashCode hashCode = default;
		hashCode.AddBytes(this[..]);
		return hashCode.ToHashCode();
	}
}
