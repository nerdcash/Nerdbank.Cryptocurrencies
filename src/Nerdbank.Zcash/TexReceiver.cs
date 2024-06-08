// Copyright (c) Andrew Arnott. All rights reserved.
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
public unsafe struct TexReceiver : IPoolReceiver, IEquatable<TexReceiver>
{
	private readonly TransparentP2PKHReceiver p2pkhReceiver;

	/// <summary>
	/// Initializes a new instance of the <see cref="TexReceiver"/> struct.
	/// </summary>
	/// <param name="p2pkh">The validating key hash.</param>
	/// <exception cref="ArgumentException">Thrown when the arguments have an unexpected length.</exception>
	public TexReceiver(ReadOnlySpan<byte> p2pkh)
	{
		this.p2pkhReceiver = new(p2pkh);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="TexReceiver"/> struct.
	/// </summary>
	/// <param name="publicKey">The EC public key to create a receiver for.</param>
	public TexReceiver(Zip32HDWallet.Transparent.ExtendedViewingKey publicKey)
	{
		this.p2pkhReceiver = new(publicKey);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="TexReceiver"/> struct.
	/// </summary>
	/// <param name="publicKey">The EC public key to create a receiver for.</param>
	public TexReceiver(Transparent.PublicKey publicKey)
	{
		this.p2pkhReceiver = new(publicKey);
	}

	/// <inheritdoc/>
	public readonly Pool Pool => Pool.Transparent;

	/// <summary>
	/// Gets the encoded representation of the entire receiver.
	/// </summary>
	[UnscopedRef]
	public readonly ReadOnlySpan<byte> Span => this.ValidatingKeyHash;

	/// <inheritdoc />
	public readonly int EncodingLength => this.Span.Length;

	/// <summary>
	/// Gets the validating key hash.
	/// </summary>
	[UnscopedRef]
	public readonly ReadOnlySpan<byte> ValidatingKeyHash => this.p2pkhReceiver.ValidatingKeyHash;

	/// <summary>
	/// Converts a <see cref="TransparentP2PKHReceiver"/> into a <see cref="TexReceiver" />.
	/// </summary>
	/// <param name="receiver">The receiver to convert from.</param>
	public static implicit operator TexReceiver(in TransparentP2PKHReceiver receiver) => new(receiver.ValidatingKeyHash);

	/// <summary>
	/// Converts a <see cref="TexReceiver"/> into a <see cref="TransparentP2PKHReceiver" />.
	/// </summary>
	/// <param name="receiver">The receiver to convert from.</param>
	public static explicit operator TransparentP2PKHReceiver(in TexReceiver receiver) => new(receiver.ValidatingKeyHash);

	/// <inheritdoc/>
	public int Encode(Span<byte> buffer) => this.Span.CopyToRetLength(buffer);

	/// <inheritdoc/>
	public bool Equals(TexReceiver other) => this.Span.SequenceEqual(other.Span);

	/// <inheritdoc/>
	public override bool Equals([NotNullWhen(true)] object? obj) => obj is TexReceiver other && this.Equals(other);

	/// <inheritdoc/>
	public override int GetHashCode()
	{
		HashCode hashCode = default;
		hashCode.AddBytes(this.Span);
		return hashCode.ToHashCode();
	}
}
