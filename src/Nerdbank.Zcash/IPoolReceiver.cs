// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash;

/// <summary>
/// An interface implemented by receivers that are embedded in Zcash addresses.
/// </summary>
public interface IPoolReceiver
{
	/// <summary>
	/// Gets the pool that this receiver can send funds into.
	/// </summary>
	Pool Pool { get; }

	/// <summary>
	/// Gets the length of the receiver's byte encoding.
	/// </summary>
	int EncodingLength { get; }

	/// <summary>
	/// Writes the entire receiver to the given buffer.
	/// </summary>
	/// <param name="buffer">The buffer to write to. Must be at least <see cref="EncodingLength"/> in length.</param>
	/// <returns>The number of bytes written to the buffer. This should always equal <see cref="EncodingLength"/>.</returns>
	int Encode(Span<byte> buffer);
}
