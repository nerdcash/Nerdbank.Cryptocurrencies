// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace Nerdbank.Zcash;

/// <summary>
/// A receiver that contains the cryptography parameters required to send Zcash to the <see cref="Pool.Sprout"/> pool.
/// </summary>
public unsafe struct SproutReceiver : IPoolReceiver, IEquatable<SproutReceiver>
{
	private const int FieldLength = 256 / 8;
	private const int Length = FieldLength * 2;
	private fixed byte backing[FieldLength * 2];

	/// <summary>
	/// Initializes a new instance of the <see cref="SproutReceiver"/> struct.
	/// </summary>
	/// <param name="apk">The a{pk} on the receiver.</param>
	/// <param name="pkEnc">The pk{enc} on the receiver.</param>
	/// <exception cref="ArgumentException">Thrown when the arguments have an unexpected length.</exception>
	public SproutReceiver(ReadOnlySpan<byte> apk, ReadOnlySpan<byte> pkEnc)
	{
		if (apk.Length != FieldLength)
		{
			throw new ArgumentException($"Length must be exactly {FieldLength}, but was {apk.Length}.", nameof(apk));
		}

		if (pkEnc.Length != FieldLength)
		{
			throw new ArgumentException($"Length must be exactly {FieldLength}, but was {pkEnc.Length}.", nameof(pkEnc));
		}

		apk.CopyTo(this.ApkWritable);
		pkEnc.CopyTo(this.PkEncWritable);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SproutReceiver"/> struct.
	/// </summary>
	/// <param name="receiver">The entire content of the receiver.</param>
	/// <exception cref="ArgumentException">Thrown when the arguments have an unexpected length.</exception>
	internal SproutReceiver(ReadOnlySpan<byte> receiver)
	{
		if (receiver.Length != Length)
		{
			throw new ArgumentException($"Length must be exactly {Length}, but was {receiver.Length}.", nameof(receiver));
		}

		receiver.CopyTo(this.SpanWritable);
	}

	/// <inheritdoc/>
	public readonly Pool Pool => Pool.Sprout;

	/// <summary>
	/// Gets the a{pk} on the receiver.
	/// </summary>
	public readonly ReadOnlySpan<byte> Apk => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in this.backing[0]), FieldLength);

	/// <summary>
	/// Gets the pk{enc} on the receiver.
	/// </summary>
	public readonly ReadOnlySpan<byte> PkEnc => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in this.backing[FieldLength]), FieldLength);

	/// <inheritdoc />
	public readonly int EncodingLength => Length;

	/// <summary>
	/// Gets the encoded representation of the entire receiver.
	/// </summary>
	[UnscopedRef]
	public readonly ReadOnlySpan<byte> Span => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in this.backing[0]), Length);

	/// <inheritdoc cref="Span" />
	[UnscopedRef]
	private Span<byte> SpanWritable => MemoryMarshal.CreateSpan(ref this.backing[0], Length);

	/// <summary>
	/// Gets the a{pk} on the receiver.
	/// </summary>
	[UnscopedRef]
	private Span<byte> ApkWritable => MemoryMarshal.CreateSpan(ref this.backing[0], FieldLength);

	/// <summary>
	/// Gets the pk{enc} on the receiver.
	/// </summary>
	[UnscopedRef]
	private Span<byte> PkEncWritable => MemoryMarshal.CreateSpan(ref this.backing[FieldLength], FieldLength);

	/// <inheritdoc/>
	public int Encode(Span<byte> buffer) => this.Span.CopyToRetLength(buffer);

	/// <inheritdoc/>
	public bool Equals(SproutReceiver other) => this.Span.SequenceEqual(other.Span);

	/// <inheritdoc/>
	public override bool Equals([NotNullWhen(true)] object? obj) => obj is SproutReceiver other && this.Equals(other);

	/// <inheritdoc/>
	public override int GetHashCode()
	{
		HashCode hashCode = default;
		hashCode.AddBytes(this.Span);
		return hashCode.ToHashCode();
	}
}
