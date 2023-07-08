// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Zcash.FixedLengthStructs;
using Nerdbank.Zcash.Orchard;

namespace Nerdbank.Zcash.Sapling;

/// <summary>
/// A viewing key that can decrypt incoming and outgoing transactions.
/// </summary>
public class FullViewingKey : IKey
{
	/// <summary>
	/// Initializes a new instance of the <see cref="FullViewingKey"/> class.
	/// </summary>
	/// <param name="spendingKey">The spending key from which to derive this full viewing key.</param>
	/// <param name="isTestNet">A value indicating whether this key is for use with testnet.</param>
	internal FullViewingKey(in ExpandedSpendingKey spendingKey, bool isTestNet)
	{
		Span<byte> fvk_bytes = stackalloc byte[96];
		if (NativeMethods.TryGetSaplingFullViewingKeyFromExpandedSpendingKey(spendingKey.ToBytes().Value, fvk_bytes) != 0)
		{
			throw new ArgumentException();
		}

		this.ViewingKey = new(new(fvk_bytes[..32]), new(fvk_bytes[32..64]));
		this.Ovk = new(fvk_bytes[64..96]);
		this.IsTestNet = isTestNet;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="FullViewingKey"/> class.
	/// </summary>
	/// <param name="viewingKey">The incoming viewing key.</param>
	/// <param name="ovk">The outgoing viewing key.</param>
	/// <param name="isTestNet">A value indicating whether this key is for use on the testnet.</param>
	internal FullViewingKey(IncomingViewingKey viewingKey, OutgoingViewingKey ovk, bool isTestNet)
	{
		this.ViewingKey = viewingKey;
		this.Ovk = ovk;
		this.IsTestNet = isTestNet;
	}

	/// <inheritdoc/>
	public bool IsTestNet { get; }

	/// <summary>
	/// Gets the viewing key.
	/// </summary>
	internal IncomingViewingKey ViewingKey { get; }

	/// <summary>
	/// Gets the Ak element.
	/// </summary>
	internal SubgroupPoint Ak => this.ViewingKey.Ak;

	/// <summary>
	/// Gets the Nk element.
	/// </summary>
	internal NullifierDerivingKey Nk => this.ViewingKey.Nk;

	/// <summary>
	/// Gets the outgoing viewing key.
	/// </summary>
	internal OutgoingViewingKey Ovk { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="FullViewingKey"/> class
	/// by deserializing it from a buffer.
	/// </summary>
	/// <param name="buffer">The 96-byte buffer to read from.</param>
	/// <param name="isTestNet">A value indicating whether this key is for use with testnet.</param>
	/// <returns>The deserialized key.</returns>
	internal static FullViewingKey FromBytes(ReadOnlySpan<byte> buffer, bool isTestNet)
	{
		SubgroupPoint ak = new(buffer[0..32]);
		NullifierDerivingKey nk = new(buffer[32..64]);
		OutgoingViewingKey ovk = new(buffer[64..96]);
		return new FullViewingKey(new IncomingViewingKey(ak, nk), ovk, isTestNet);
	}

	/// <summary>
	/// Gets the raw encoding.
	/// </summary>
	/// <param name="rawEncoding">Receives the raw encoding. Must be at least 96 bytes in length.</param>
	/// <returns>The number of bytes written to <paramref name="rawEncoding"/>. Always 96.</returns>
	/// <remarks>
	/// As specified in the <see href="https://zips.z.cash/protocol/protocol.pdf">Zcash protocol spec section 5.6.3.3</see>.
	/// </remarks>
	internal int ToBytes(Span<byte> rawEncoding)
	{
		int written = 0;
		written += this.Ak.Value.CopyToRetLength(rawEncoding[written..]);
		written += this.Nk.Value.CopyToRetLength(rawEncoding[written..]);
		written += this.Ovk.Value.CopyToRetLength(rawEncoding[written..]);
		return written;
	}

	/// <summary>
	/// Gets the raw encoding.
	/// </summary>
	/// <returns>The raw encoding.</returns>
	/// <remarks>
	/// As specified in the <see href="https://zips.z.cash/protocol/protocol.pdf">Zcash protocol spec section 5.6.3.3</see>.
	/// </remarks>
	internal Bytes96 ToBytes()
	{
		Span<byte> result = stackalloc byte[96];
		this.ToBytes(result);
		return new(result);
	}
}
