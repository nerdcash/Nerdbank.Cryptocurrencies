// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

namespace Nerdbank.Zcash;

/// <summary>
/// The ID of a Zcash transaction.
/// </summary>
public struct TxId : IEquatable<TxId>
{
	/// <summary>
	/// The length of the value in bytes.
	/// </summary>
	public const int Length = 32;

	private readonly Bytes32 txid;

	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	private string? txidString;

	/// <summary>
	/// Initializes a new instance of the <see cref="TxId"/> struct.
	/// </summary>
	/// <param name="value">The bytes containing the value. This should have a length equal to <see cref="Length"/>.</param>
	public TxId(ReadOnlySpan<byte> value)
	{
		this.txid = new Bytes32(value);
	}

	/// <summary>
	/// Gets the raw bytes of the transaction id.
	/// </summary>
	/// <param name="range">The range of the bytes to fetch.</param>
	/// <returns>A span into the txid bytes.</returns>
	[UnscopedRef]
	public readonly ReadOnlySpan<byte> this[Range range] => this.txid[range];

	/// <summary>
	/// Extracts the raw bytes of the transaction id.
	/// </summary>
	/// <param name="txid">The transaction ID to export.</param>
	public static implicit operator ReadOnlySpan<byte>(in TxId txid) => txid[..];

	/// <summary>
	/// Parses a transaction ID from a string.
	/// </summary>
	/// <param name="txid">The text version of the TxId. This is expected to be the hex encoding of the bytes in reverse order.</param>
	/// <returns>The parsed TxId.</returns>
	public static TxId Parse(ReadOnlySpan<char> txid)
	{
		Requires.Argument(txid.Length == Length * 2, nameof(txid), "Unexpected length");
		Span<byte> bytes = stackalloc byte[Length];
		for (int i = 0; i < Length; i++)
		{
			bytes[Length - i - 1] = byte.Parse(txid.Slice(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
		}

		return new TxId(bytes);
	}

	/// <inheritdoc cref="Parse(ReadOnlySpan{char})"/>
	public static TxId Parse(string txid)
	{
		TxId result = Parse(txid.AsSpan());
		result.txidString = txid;
		return result;
	}

	/// <inheritdoc/>
	bool IEquatable<TxId>.Equals(TxId other) => this.txid.Equals(other.txid);

	/// <inheritdoc cref="IEquatable{T}.Equals"/>
	public readonly bool Equals(in TxId other) => this.txid.Equals(other.txid);

	/// <inheritdoc/>
	public override string ToString()
	{
		if (this.txidString is null)
		{
			// txid's are traditionally rendered as hex, in the opposite order of the bytes in the txid.
			Span<byte> txidReversed = stackalloc byte[Length];
			this.txid[..].CopyTo(txidReversed);
			txidReversed.Reverse();

			// For .NET 9: switch to Convert.ToHexStringLower
			this.txidString = Convert.ToHexString(txidReversed).ToLowerInvariant();
		}

		return this.txidString;
	}

	/// <summary>
	/// Forces the string representation of this <see cref="TxId"/> to be cached.
	/// </summary>
	/// <returns>The <c>this</c> pointer to the struct with the cached string.</returns>
	/// <remarks>
	/// This is useful when this struct is about to be stored in a readonly field in order to avoid allocating a new string each time it is called.
	/// </remarks>
	internal TxId PrecacheString()
	{
		this.ToString();
		return this;
	}
}
