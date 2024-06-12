// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace Nerdbank.Zcash.Sapling;

/// <summary>
/// A subgroup point.
/// </summary>
[InlineArray(Length)]
internal struct SubgroupPoint : IEquatable<SubgroupPoint>
{
	/// <summary>
	/// The length of the value in bytes.
	/// </summary>
	public const int Length = 32;

	private byte element;

	/// <summary>
	/// Initializes a new instance of the <see cref="SubgroupPoint"/> struct.
	/// </summary>
	/// <param name="value">The bytes containing the value. This should have a length equal to <see cref="Length"/>.</param>
	internal SubgroupPoint(ReadOnlySpan<byte> value)
	{
		value.CopyToWithLengthCheck(this);
	}

	/// <summary>
	/// Returns a strongly-typed struct over a span of bytes without incuring the cost of a memory copy.
	/// </summary>
	/// <param name="value">The bytes containing the value. This should have a length equal to <see cref="Length"/>.</param>
	/// <returns>The strongly-typed element.</returns>
	public static ref readonly SubgroupPoint From(ReadOnlySpan<byte> value) => ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, SubgroupPoint>(value));

	/// <inheritdoc/>
	readonly bool IEquatable<SubgroupPoint>.Equals(SubgroupPoint other) => this[..].SequenceEqual(other);

	/// <inheritdoc cref="IEquatable{T}.Equals"/>
	public readonly bool Equals(in SubgroupPoint other) => this[..].SequenceEqual(other);
}
