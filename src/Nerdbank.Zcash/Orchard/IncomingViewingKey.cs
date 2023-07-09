// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash.Orchard;

/// <summary>
/// The incoming viewing key for the Orchard pool.
/// </summary>
internal struct IncomingViewingKey
{
	/// <summary>
	/// Initializes a new instance of the <see cref="IncomingViewingKey"/> struct.
	/// </summary>
	/// <param name="dk">The diversifier key.</param>
	/// <param name="ivk">The key agreement private key.</param>
	internal IncomingViewingKey(DiversifierKey dk, KeyAgreementPrivateKey ivk)
	{
		this.Dk = dk;
		this.Ivk = ivk;
	}

	/// <summary>
	/// Gets the diversifier key.
	/// </summary>
	internal DiversifierKey Dk { get; }

	/// <summary>
	/// Gets the key agreement private key.
	/// </summary>
	internal KeyAgreementPrivateKey Ivk { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="IncomingViewingKey"/> struct
	/// from its raw encoding.
	/// </summary>
	/// <param name="buffer">The buffer to read from. 64-bytes will be read from this.</param>
	/// <returns>The incoming viewing key.</returns>
	internal static IncomingViewingKey Decode(ReadOnlySpan<byte> buffer) => new(new DiversifierKey(buffer[..32]), new KeyAgreementPrivateKey(buffer[32..64]));

	/// <summary>
	/// Writes out the raw encoding of the incoming viewing key.
	/// </summary>
	/// <param name="buffer">The buffer to write to. Must be at least 64 bytes.</param>
	/// <returns>The number of bytes written. Always 64.</returns>
	internal int Encode(Span<byte> buffer)
	{
		int written = 0;
		written += this.Dk.Value.CopyToRetLength(buffer[written..]);
		written += this.Ivk.Value.CopyToRetLength(buffer[written..]);
		return written;
	}
}
