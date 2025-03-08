// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash;

/// <summary>
/// A receiver that contains the cryptography parameters required to send Zcash to the <see cref="Pool.Sprout"/> pool.
/// </summary>
[InlineArray(Length)]
public struct SproutReceiver : IPoolReceiver, IEquatable<SproutReceiver>
{
	/// <summary>
	/// Gets the length of the receiver, in bytes.
	/// </summary>
	public const int Length = FieldLength * 2;

	private const int FieldLength = 256 / 8;

	private byte backing;

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

		apk.CopyTo(this[..FieldLength]);
		pkEnc.CopyTo(this[FieldLength..]);
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

		receiver.CopyTo(this);
	}

	/// <inheritdoc/>
	public readonly Pool Pool => Pool.Sprout;

	/// <summary>
	/// Gets the a{pk} on the receiver.
	/// </summary>
	[UnscopedRef]
	public readonly ReadOnlySpan<byte> Apk => this[..FieldLength];

	/// <summary>
	/// Gets the pk{enc} on the receiver.
	/// </summary>
	[UnscopedRef]
	public readonly ReadOnlySpan<byte> PkEnc => this[FieldLength..];

	/// <inheritdoc />
	readonly int IPoolReceiver.EncodingLength => Length;

	/// <inheritdoc/>
	public readonly int Encode(Span<byte> buffer) => this[..].CopyToRetLength(buffer);

	/// <inheritdoc/>
	readonly bool IEquatable<SproutReceiver>.Equals(SproutReceiver other) => this.Equals(other);

	/// <inheritdoc cref="IEquatable{T}.Equals(T)"/>
	public readonly bool Equals(in SproutReceiver other) => this[..].SequenceEqual(other[..]);

	/// <inheritdoc/>
	public override readonly bool Equals([NotNullWhen(true)] object? obj) => obj is SproutReceiver other && this.Equals(other);

	/// <inheritdoc/>
	public override readonly int GetHashCode()
	{
		HashCode hashCode = default;
		hashCode.AddBytes(this);
		return hashCode.ToHashCode();
	}
}
