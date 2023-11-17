// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.InteropServices;

namespace Nerdbank.Zcash;

/// <summary>
/// A buffer that contains the diversifier index (i.e. the unencrypted diversifier)
/// used to generate diversified addresses in sapling and orchard pools.
/// </summary>
[InlineArray(Length)]
public struct DiversifierIndex : IEquatable<DiversifierIndex>
{
	/// <summary>
	/// The length of the value in bytes.
	/// </summary>
	public const int Length = 11;

	private byte element;

	/// <summary>
	/// Initializes a new instance of the <see cref="DiversifierIndex"/> struct.
	/// </summary>
	/// <param name="value">The diversifier index value. This must be an 11-byte buffer.</param>
	public DiversifierIndex(ReadOnlySpan<byte> value)
	{
		value.CopyToWithLengthCheck(this, nameof(value));
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="DiversifierIndex"/> struct.
	/// </summary>
	/// <param name="value">
	/// The big integer to use as the diversifier index.
	/// This value must be representable as an unsigned integer with no more than 11 bytes of memory.
	/// </param>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="value"/> is negative.</exception>
	public DiversifierIndex(BigInteger value)
	{
		if (!value.TryWriteBytes(this, out _, isUnsigned: true))
		{
			throw new ArgumentException("Index must fit within 11 bytes.", nameof(value));
		}
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="DiversifierIndex"/> struct.
	/// </summary>
	/// <param name="value">The diversifier index.</param>
	public DiversifierIndex(ulong value)
	{
		BitUtilities.WriteLE(value, this);
	}

	/// <summary>
	/// Creates a <see cref="DiversifierIndex"/> value based on a <see cref="BigInteger"/>.
	/// </summary>
	/// <param name="diversifierIndex">
	/// The big integer.
	/// This value must be representable as an unsigned integer with no more than 11 bytes of memory.
	/// </param>
	public static implicit operator DiversifierIndex(BigInteger diversifierIndex) => new(diversifierIndex);

	/// <summary>
	/// Creates a <see cref="DiversifierIndex"/> value based on a <see cref="ulong"/>.
	/// </summary>
	/// <param name="diversifierIndex">The index.</param>
	public static implicit operator DiversifierIndex(ulong diversifierIndex) => new(diversifierIndex);

	/// <summary>
	/// Returns a strongly-typed struct over a span of bytes without incuring the cost of a memory copy.
	/// </summary>
	/// <param name="value">The bytes containing the value. This should have a length equal to <see cref="Length"/>.</param>
	/// <returns>The strongly-typed element.</returns>
	public static ref readonly DiversifierIndex From(ReadOnlySpan<byte> value) => ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, DiversifierIndex>(value));

	/// <summary>
	/// Creates a big integer based on this diversifier index value.
	/// </summary>
	/// <returns>The big integer.</returns>
	public readonly BigInteger ToBigInteger() => new BigInteger(this, isUnsigned: true);

	/// <inheritdoc/>
	bool IEquatable<DiversifierIndex>.Equals(DiversifierIndex other) => this[..].SequenceEqual(other);

	/// <inheritdoc cref="IEquatable{T}.Equals"/>
	public readonly bool Equals(in DiversifierIndex other) => this[..].SequenceEqual(other);

	/// <inheritdoc/>
	public override string ToString() => Convert.ToHexString(this);
}
