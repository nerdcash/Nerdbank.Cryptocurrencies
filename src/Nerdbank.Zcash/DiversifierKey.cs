// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace Nerdbank.Zcash;

/// <summary>
/// A diversifier key.
/// </summary>
[InlineArray(Length)]
internal struct DiversifierKey : IEquatable<DiversifierKey>
{
	/// <summary>
	/// The length of the value in bytes.
	/// </summary>
	public const int Length = 32;

	private byte element;

	/// <summary>
	/// Initializes a new instance of the <see cref="DiversifierKey"/> struct.
	/// </summary>
	/// <param name="value">The bytes containing the value. This should have a length equal to <see cref="Length"/>.</param>
	public DiversifierKey(ReadOnlySpan<byte> value)
	{
		value.CopyToWithLengthCheck(this);
	}

	/// <summary>
	/// Returns a strongly-typed struct over a span of bytes without incuring the cost of a memory copy.
	/// </summary>
	/// <param name="value">The bytes containing the value. This should have a length equal to <see cref="Length"/>.</param>
	/// <returns>The strongly-typed element.</returns>
	public static ref readonly DiversifierKey From(ReadOnlySpan<byte> value) => ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, DiversifierKey>(value));

	/// <inheritdoc/>
	bool IEquatable<DiversifierKey>.Equals(DiversifierKey other) => this[..].SequenceEqual(other);

	/// <inheritdoc cref="IEquatable{T}.Equals"/>
	public readonly bool Equals(in DiversifierKey other) => this[..].SequenceEqual(other);
}
