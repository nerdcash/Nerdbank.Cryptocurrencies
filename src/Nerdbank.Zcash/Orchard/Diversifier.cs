// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace Nerdbank.Zcash.Orchard;

/// <summary>
/// The diversifier used when constructing an <see cref="OrchardReceiver"/> from a <see cref="FullViewingKey"/>.
/// </summary>
[InlineArray(Length)]
internal struct Diversifier : IEquatable<Diversifier>
{
	/// <summary>
	/// The length of the value in bytes.
	/// </summary>
	public const int Length = 11;

	private byte element;

	/// <summary>
	/// Initializes a new instance of the <see cref="Diversifier"/> struct.
	/// </summary>
	/// <param name="value">The value of the buffer.</param>
	internal Diversifier(ReadOnlySpan<byte> value)
	{
		value.CopyToWithLengthCheck(this, nameof(value));
	}

	/// <summary>
	/// Returns a strongly-typed struct over a span of bytes without incuring the cost of a memory copy.
	/// </summary>
	/// <param name="value">The bytes containing the value. This should have a length equal to <see cref="Length"/>.</param>
	/// <returns>The strongly-typed element.</returns>
	public static ref readonly Diversifier From(ReadOnlySpan<byte> value) => ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, Diversifier>(value));

	/// <inheritdoc/>
	readonly bool IEquatable<Diversifier>.Equals(Diversifier other) => this[..].SequenceEqual(other);

	/// <inheritdoc cref="IEquatable{T}.Equals"/>
	public readonly bool Equals(in Diversifier other) => this[..].SequenceEqual(other);
}
