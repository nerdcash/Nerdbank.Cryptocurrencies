// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace Nerdbank.Zcash;

/// <summary>
/// A receiver that contains the cryptography parameters required to send Zcash to the <see cref="Pool.Transparent"/> pool
/// by way of a Pay to Public Key Hash method.
/// </summary>
public unsafe struct TransparentP2PKHReceiver : IUnifiedPoolReceiver, IEquatable<TransparentP2PKHReceiver>
{
	private const int Length = 160 / 8;

	private fixed byte validatingKeyHash[Length];

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

		p2pkh.CopyTo(this.ValidatingKeyHashWritable);
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

		Assumes.True(Bitcoin.PublicKey.CreatePublicKeyHash(serializedPublicKey, this.ValidatingKeyHashWritable) == this.ValidatingKeyHashWritable.Length);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="TransparentP2PKHReceiver"/> struct.
	/// </summary>
	/// <param name="publicKey">The EC public key to create a receiver for.</param>
	public TransparentP2PKHReceiver(Transparent.PublicKey publicKey)
	{
		Requires.NotNull(publicKey);

		Assumes.True(Bitcoin.PublicKey.CreatePublicKeyHash(publicKey.KeyMaterial, this.ValidatingKeyHashWritable) == this.ValidatingKeyHashWritable.Length);
	}

	/// <inheritdoc cref="IUnifiedPoolReceiver.UnifiedReceiverTypeCode"/>
	public static byte UnifiedReceiverTypeCode => UnifiedTypeCodes.Sapling;

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
	public readonly ReadOnlySpan<byte> ValidatingKeyHash => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in this.validatingKeyHash[0]), Length);

	/// <summary>
	/// Gets the validating key hash.
	/// </summary>
	[UnscopedRef]
	private Span<byte> ValidatingKeyHashWritable => MemoryMarshal.CreateSpan(ref this.validatingKeyHash[0], Length);

	/// <inheritdoc/>
	public int Encode(Span<byte> buffer) => this.Span.CopyToRetLength(buffer);

	/// <inheritdoc/>
	public bool Equals(TransparentP2PKHReceiver other) => this.Span.SequenceEqual(other.Span);

	/// <inheritdoc/>
	public override bool Equals([NotNullWhen(true)] object? obj) => obj is TransparentP2PKHReceiver other && this.Equals(other);

	/// <inheritdoc/>
	public override int GetHashCode()
	{
		HashCode hashCode = default;
		hashCode.AddBytes(this.Span);
		return hashCode.ToHashCode();
	}
}
