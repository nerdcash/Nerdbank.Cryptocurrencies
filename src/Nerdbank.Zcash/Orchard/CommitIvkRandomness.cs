// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Zcash.FixedLengthStructs;

namespace Nerdbank.Zcash.Orchard;

/// <summary>
/// The IVK commit randomness.
/// </summary>
internal readonly struct CommitIvkRandomness
{
	private readonly Bytes32 value;

	internal CommitIvkRandomness(ReadOnlySpan<byte> value)
	{
		this.value = new(value);
	}

	/// <summary>
	/// Gets the buffer. Always 32 bytes in length.
	/// </summary>
	internal readonly ReadOnlySpan<byte> Value => this.value.Value;
}
