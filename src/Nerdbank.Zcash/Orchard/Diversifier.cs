// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Zcash.FixedLengthStructs;

namespace Nerdbank.Zcash.Orchard;

/// <summary>
/// The diversifier used when constructing an <see cref="OrchardReceiver"/> from a <see cref="FullViewingKey"/>.
/// </summary>
internal readonly struct Diversifier
{
	private readonly Bytes11 value;

	/// <summary>
	/// Initializes a new instance of the <see cref="Diversifier"/> struct.
	/// </summary>
	/// <param name="value">The value of the buffer.</param>
	internal Diversifier(ReadOnlySpan<byte> value)
	{
		this.value = new(value);
	}

	/// <summary>
	/// Gets the buffer. Always 11 bytes in length.
	/// </summary>
	internal readonly ReadOnlySpan<byte> Value => this.value.Value;
}
