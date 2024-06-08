// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace Nerdbank.Zcash;

/// <summary>
/// A receiver that contains the cryptography parameters required to send Zcash to the <see cref="Pool.Orchard"/> pool.
/// </summary>
public unsafe struct OrchardReceiver : IUnifiedPoolReceiver, IEquatable<OrchardReceiver>
{
	private const int DLength = 88 / 8;
	private const int PkdLength = 256 / 8;
	private const int Length = DLength + PkdLength;
	private fixed byte backing[Length];

	/// <summary>
	/// Initializes a new instance of the <see cref="OrchardReceiver"/> struct.
	/// </summary>
	/// <param name="d">The LEBS2OSP(d) on the receiver.</param>
	/// <param name="pkd">The LEBS2OSP(pkd) on the receiver.</param>
	/// <exception cref="ArgumentException">Thrown when the arguments have an unexpected length.</exception>
	public OrchardReceiver(ReadOnlySpan<byte> d, ReadOnlySpan<byte> pkd)
	{
		if (d.Length != DLength)
		{
			throw new ArgumentException($"Length must be exactly {DLength}, but was {d.Length}.", nameof(d));
		}

		if (pkd.Length != PkdLength)
		{
			throw new ArgumentException($"Length must be exactly {PkdLength}, but was {pkd.Length}.", nameof(pkd));
		}

		d.CopyTo(this.DWritable);
		pkd.CopyTo(this.PkdWritable);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="OrchardReceiver"/> struct.
	/// </summary>
	/// <param name="receiver">The entire content of the receiver.</param>
	/// <exception cref="ArgumentException">Thrown when the arguments have an unexpected length.</exception>
	internal OrchardReceiver(ReadOnlySpan<byte> receiver)
	{
		if (receiver.Length != Length)
		{
			throw new ArgumentException($"Length must be exactly {Length}, but was {receiver.Length}.", nameof(receiver));
		}

		receiver.CopyTo(this.SpanWritable);
	}

	/// <inheritdoc cref="IUnifiedPoolReceiver.UnifiedReceiverTypeCode"/>
	public static byte UnifiedReceiverTypeCode => UnifiedTypeCodes.Orchard;

	/// <inheritdoc/>
	public readonly Pool Pool => Pool.Orchard;

	/// <summary>
	/// Gets the LEBS2OSP(d) on the receiver.
	/// </summary>
	[UnscopedRef]
	public readonly ReadOnlySpan<byte> D => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in this.backing[0]), DLength);

	/// <summary>
	/// Gets the LEBS2OSP(repr(pkd)) on the receiver.
	/// </summary>
	[UnscopedRef]
	public readonly ReadOnlySpan<byte> Pkd => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in this.backing[DLength]), PkdLength);

	/// <summary>
	/// Gets the encoded representation of the entire receiver.
	/// </summary>
	[UnscopedRef]
	public readonly ReadOnlySpan<byte> Span => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in this.backing[0]), Length);

	/// <inheritdoc />
	public readonly int EncodingLength => Length;

	/// <inheritdoc cref="Span" />
	[UnscopedRef]
	private Span<byte> SpanWritable => MemoryMarshal.CreateSpan(ref this.backing[0], Length);

	/// <summary>
	/// Gets the LEBS2OSP(d) on the receiver.
	/// </summary>
	[UnscopedRef]
	private Span<byte> DWritable => MemoryMarshal.CreateSpan(ref this.backing[0], DLength);

	/// <summary>
	/// Gets the LEBS2OSP(repr(pkd)) on the receiver.
	/// </summary>
	[UnscopedRef]
	private Span<byte> PkdWritable => MemoryMarshal.CreateSpan(ref this.backing[DLength], PkdLength);

	/// <inheritdoc/>
	public int Encode(Span<byte> buffer) => this.Span.CopyToRetLength(buffer);

	/// <inheritdoc/>
	public bool Equals(OrchardReceiver other) => this.Span.SequenceEqual(other.Span);

	/// <inheritdoc/>
	public override bool Equals([NotNullWhen(true)] object? obj) => obj is OrchardReceiver other && this.Equals(other);

	/// <inheritdoc/>
	public override int GetHashCode()
	{
		HashCode hashCode = default;
		hashCode.AddBytes(this.Span);
		return hashCode.ToHashCode();
	}
}
