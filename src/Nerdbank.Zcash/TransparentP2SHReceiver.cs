// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash;

/// <summary>
/// A receiver that contains the cryptography parameters required to send Zcash to the <see cref="Pool.Transparent"/> pool
/// by way of a Pay to Script Hash method.
/// </summary>
[InlineArray(Length)]
public struct TransparentP2SHReceiver : IUnifiedPoolReceiver, IEquatable<TransparentP2SHReceiver>
{
	/// <summary>
	/// Gets the length of the receiver, in bytes.
	/// </summary>
	public const int Length = 160 / 8;

	private byte scriptHash;

	/// <summary>
	/// Initializes a new instance of the <see cref="TransparentP2SHReceiver"/> struct.
	/// </summary>
	/// <param name="p2sh">The script hash.</param>
	/// <exception cref="ArgumentException">Thrown when the arguments have an unexpected length.</exception>
	public TransparentP2SHReceiver(ReadOnlySpan<byte> p2sh)
	{
		if (p2sh.Length != Length)
		{
			throw new ArgumentException($"Length must be exactly {Length}, but was {p2sh.Length}.", nameof(p2sh));
		}

		p2sh.CopyTo(this);
	}

	/// <summary>
	/// Gets a span over the whole receiver.
	/// </summary>
	/// <inheritdoc cref="IUnifiedPoolReceiver.UnifiedReceiverTypeCode"/>
	public static byte UnifiedReceiverTypeCode => UnifiedTypeCodes.TransparentP2SH;

	/// <inheritdoc/>
	public readonly Pool Pool => Pool.Transparent;

	/// <inheritdoc />
	readonly int IPoolReceiver.EncodingLength => Length;

	/// <inheritdoc/>
	public readonly int Encode(Span<byte> buffer) => this[..].CopyToRetLength(buffer);

	/// <inheritdoc/>
	readonly bool IEquatable<TransparentP2SHReceiver>.Equals(TransparentP2SHReceiver other) => this.Equals(other);

	/// <inheritdoc cref="IEquatable{T}.Equals(T)"/>
	public readonly bool Equals(in TransparentP2SHReceiver other) => this[..].SequenceEqual(other[..]);

	/// <inheritdoc/>
	public override readonly bool Equals([NotNullWhen(true)] object? obj) => obj is TransparentP2SHReceiver other && this.Equals(other);

	/// <inheritdoc/>
	public override readonly int GetHashCode()
	{
		HashCode hashCode = default;
		hashCode.AddBytes(this);
		return hashCode.ToHashCode();
	}
}
