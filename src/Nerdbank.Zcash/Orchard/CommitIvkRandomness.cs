// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace Nerdbank.Zcash.Orchard;

/// <summary>
/// The IVK commit randomness.
/// </summary>
[InlineArray(Length)]
internal struct CommitIvkRandomness : IEquatable<CommitIvkRandomness>
{
	/// <summary>
	/// The length of the value in bytes.
	/// </summary>
	public const int Length = 32;

	private byte element;

	/// <summary>
	/// Initializes a new instance of the <see cref="CommitIvkRandomness"/> struct.
	/// </summary>
	/// <param name="value">The bytes containing the value. This should have a length equal to <see cref="Length"/>.</param>
	public CommitIvkRandomness(ReadOnlySpan<byte> value)
	{
		value.CopyToWithLengthCheck(this);
	}

	/// <summary>
	/// Returns a strongly-typed struct over a span of bytes without incuring the cost of a memory copy.
	/// </summary>
	/// <param name="value">The bytes containing the value. This should have a length equal to <see cref="Length"/>.</param>
	/// <returns>The strongly-typed element.</returns>
	public static ref readonly CommitIvkRandomness From(ReadOnlySpan<byte> value) => ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, CommitIvkRandomness>(value));

	/// <inheritdoc/>
	readonly bool IEquatable<CommitIvkRandomness>.Equals(CommitIvkRandomness other) => this[..].SequenceEqual(other);

	/// <inheritdoc cref="IEquatable{T}.Equals"/>
	public readonly bool Equals(in CommitIvkRandomness other) => this[..].SequenceEqual(other);
}
