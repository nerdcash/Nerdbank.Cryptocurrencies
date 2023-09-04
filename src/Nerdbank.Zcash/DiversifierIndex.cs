// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Numerics;
using Nerdbank.Zcash.FixedLengthStructs;

namespace Nerdbank.Zcash;

/// <summary>
/// A buffer that contains the diversifier index (i.e. the unencrypted diversifier)
/// used to generate diversified addresses in sapling and orchard pools.
/// </summary>
public struct DiversifierIndex : IEquatable<DiversifierIndex>
{
	private readonly Bytes11 value;

	/// <summary>
	/// Initializes a new instance of the <see cref="DiversifierIndex"/> struct.
	/// </summary>
	/// <param name="value">The diversifier index value. This must be an 11-byte buffer.</param>
	public DiversifierIndex(ReadOnlySpan<byte> value)
	{
		value.CopyToWithLengthCheck(this.value.ValueWritable, nameof(value));
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
		if (!value.TryWriteBytes(this.value.ValueWritable, out _, isUnsigned: true))
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
		BitUtilities.WriteLE(value, this.value.ValueWritable);
	}

	/// <summary>
	/// Gets the writable diversifier index buffer.
	/// </summary>
	[UnscopedRef]
	public readonly ReadOnlySpan<byte> Value => this.value.Value;

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
	/// Creates a big integer based on this diversifier index value.
	/// </summary>
	/// <returns>The big integer.</returns>
	public readonly BigInteger ToBigInteger() => new BigInteger(this.Value, isUnsigned: true);

	/// <inheritdoc/>
	public bool Equals(DiversifierIndex other) => this.value.Value.SequenceEqual(other.value.Value);
}
