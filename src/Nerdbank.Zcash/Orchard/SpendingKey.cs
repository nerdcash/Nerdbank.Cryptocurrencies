// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Zcash.FixedLengthStructs;

namespace Nerdbank.Zcash.Orchard;

/// <summary>
/// A spending key.
/// </summary>
internal readonly struct SpendingKey
{
	private readonly Bytes32 value;

	/// <summary>
	/// Initializes a new instance of the <see cref="SpendingKey"/> struct.
	/// </summary>
	/// <param name="value">The 32-byte secret.</param>
	internal SpendingKey(ReadOnlySpan<byte> value)
	{
		this.value = new(value);
	}

	/// <summary>
	/// Gets the buffer. Always 32 bytes in length.
	/// </summary>
	[UnscopedRef]
	internal readonly ReadOnlySpan<byte> Value => this.value.Value;
}
