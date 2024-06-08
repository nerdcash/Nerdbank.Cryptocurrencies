// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace Nerdbank.Zcash;

/// <summary>
/// A 96-byte buffer.
/// </summary>
[InlineArray(Length)]
internal struct Bytes96 : IEquatable<Bytes96>
{
	/// <summary>
	/// The length of the value in bytes.
	/// </summary>
	public const int Length = 96;

	private byte element;

	/// <summary>
	/// Initializes a new instance of the <see cref="Bytes96"/> struct.
	/// </summary>
	/// <param name="value">The bytes containing the value. This should have a length equal to <see cref="Length"/>.</param>
	internal Bytes96(ReadOnlySpan<byte> value)
	{
		value.CopyToWithLengthCheck(this);
	}

	/// <summary>
	/// Returns a strongly-typed struct over a span of bytes without incuring the cost of a memory copy.
	/// </summary>
	/// <param name="value">The bytes containing the value. This should have a length equal to <see cref="Length"/>.</param>
	/// <returns>The strongly-typed element.</returns>
	public static ref readonly Bytes96 From(ReadOnlySpan<byte> value) => ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, Bytes96>(value));

	/// <inheritdoc/>
	readonly bool IEquatable<Bytes96>.Equals(Bytes96 other) => this[..].SequenceEqual(other);

	/// <inheritdoc cref="IEquatable{T}.Equals"/>
	public readonly bool Equals(in Bytes96 other) => this[..].SequenceEqual(other);
}
