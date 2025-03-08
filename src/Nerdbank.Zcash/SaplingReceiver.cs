// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash;

/// <summary>
/// A receiver that contains the cryptography parameters required to send Zcash to the <see cref="Pool.Sapling"/> pool.
/// </summary>
[InlineArray(Length)]
public struct SaplingReceiver : IUnifiedPoolReceiver, IEquatable<SaplingReceiver>
{
	/// <summary>
	/// Gets the length of the receiver, in bytes.
	/// </summary>
	public const int Length = 11 + 32;

	private const int DLength = 11;
	private const int PkdLength = 32;
	private byte backing;

	/// <summary>
	/// Initializes a new instance of the <see cref="SaplingReceiver"/> struct.
	/// </summary>
	/// <param name="d">The LEBS2OSP(d) on the receiver.</param>
	/// <param name="pkd">The LEBS2OSP(pkd) on the receiver.</param>
	/// <exception cref="ArgumentException">Thrown when the arguments have an unexpected length.</exception>
	public SaplingReceiver(ReadOnlySpan<byte> d, ReadOnlySpan<byte> pkd)
	{
		if (d.Length != DLength)
		{
			throw new ArgumentException($"Length must be exactly {DLength}, but was {d.Length}.", nameof(d));
		}

		if (pkd.Length != PkdLength)
		{
			throw new ArgumentException($"Length must be exactly {PkdLength}, but was {pkd.Length}.", nameof(pkd));
		}

		d.CopyTo(this[..DLength]);
		pkd.CopyTo(this[DLength..]);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SaplingReceiver"/> struct.
	/// </summary>
	/// <param name="receiver">The entire content of the receiver.</param>
	/// <exception cref="ArgumentException">Thrown when the arguments have an unexpected length.</exception>
	internal SaplingReceiver(ReadOnlySpan<byte> receiver)
	{
		if (receiver.Length != Length)
		{
			throw new ArgumentException($"Length must be exactly {Length}, but was {receiver.Length}.", nameof(receiver));
		}

		receiver.CopyTo(this);
	}

	/// <inheritdoc cref="IUnifiedPoolReceiver.UnifiedReceiverTypeCode"/>
	public static byte UnifiedReceiverTypeCode => UnifiedTypeCodes.Sapling;

	/// <inheritdoc/>
	public readonly Pool Pool => Pool.Sapling;

	/// <summary>
	/// Gets the LEBS2OSP(d) on the receiver.
	/// </summary>
	[UnscopedRef]
	public readonly ReadOnlySpan<byte> D => this[..DLength];

	/// <summary>
	/// Gets the LEBS2OSP(repr(pkd)) on the receiver.
	/// </summary>
	[UnscopedRef]
	public readonly ReadOnlySpan<byte> Pkd => this[DLength..];

	/// <inheritdoc />
	readonly int IPoolReceiver.EncodingLength => Length;

	/// <inheritdoc/>
	public readonly int Encode(Span<byte> buffer) => this[..].CopyToRetLength(buffer);

	/// <inheritdoc/>
	readonly bool IEquatable<SaplingReceiver>.Equals(SaplingReceiver other) => this.Equals(other);

	/// <inheritdoc cref="IEquatable{T}.Equals(T)"/>
	public readonly bool Equals(in SaplingReceiver other) => this[..].SequenceEqual(other[..]);

	/// <inheritdoc/>
	public override readonly bool Equals([NotNullWhen(true)] object? obj) => obj is SaplingReceiver other && this.Equals(other);

	/// <inheritdoc/>
	public override readonly int GetHashCode()
	{
		HashCode hashCode = default;
		hashCode.AddBytes(this);
		return hashCode.ToHashCode();
	}
}
