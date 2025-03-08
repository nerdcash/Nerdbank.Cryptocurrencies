﻿// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash;

/// <summary>
/// A memo field in a Zcash transaction.
/// </summary>
/// <remarks>
/// The methods that encode/decode the memo field assume a ZIP-302 conformant memo.
/// </remarks>
[DebuggerDisplay($"{{{nameof(DebuggerDisplay)},nq}}")]
public struct Memo : IEquatable<Memo>
{
	private Bytes512 bytes;

	/// <summary>
	/// Initializes a new instance of the <see cref="Memo"/> struct.
	/// </summary>
	/// <param name="memoBytes">The memo bytes. Must be at most 512 bytes. Fewer bytes will lead to padding zeros to fill 512 bytes.</param>
	public Memo(ReadOnlySpan<byte> memoBytes)
	{
		memoBytes.CopyTo(this.bytes);
	}

	/// <summary>
	/// Gets a memo that explicitly communicates no memo was given.
	/// </summary>
	public static Memo NoMemo
	{
		get
		{
			Memo memo = default;
			memo.Clear();
			return memo;
		}
	}

	/// <summary>
	/// Gets or sets a human readable message in the memo.
	/// </summary>
	/// <remarks>
	/// If the memo doesn't contain a human readable message, the value of the property is <see langword="null" />.
	/// </remarks>
	public string? Message
	{
		readonly get => Zip302MemoFormat.TryDecodeMessage(this.bytes, out string? text) ? text : null;
		set => Zip302MemoFormat.EncodeMessage(value, this.bytes);
	}

	/// <summary>
	/// Gets or sets the proprietary data in the memo.
	/// </summary>
	/// <remarks>
	/// If the memo doesn't contain proprietary data, the value of the property is an empty span.
	/// </remarks>
	public ReadOnlySpan<byte> ProprietaryData
	{
		[UnscopedRef]
		readonly get => Zip302MemoFormat.DetectMemoFormat(this.bytes) == Zip302MemoFormat.MemoFormat.ProprietaryData ? this.bytes[1..] : default;
		set => Zip302MemoFormat.EncodeProprietaryData(value, this.bytes);
	}

	/// <summary>
	/// Gets the raw 512 byte memo.
	/// </summary>
	[UnscopedRef]
	public readonly ReadOnlySpan<byte> RawBytes => this.bytes;

	/// <summary>
	/// Gets a value indicating whether this memo is empty.
	/// </summary>
	public readonly bool IsEmpty => Zip302MemoFormat.DetectMemoFormat(this.bytes) == Zip302MemoFormat.MemoFormat.NoMemo;

	/// <inheritdoc cref="Zip302MemoFormat.DetectMemoFormat"/>
	public readonly Zip302MemoFormat.MemoFormat MemoFormat => Zip302MemoFormat.DetectMemoFormat(this.bytes);

	private readonly string DebuggerDisplay => this.ToString(useQuotesAroundMessage: true);

	/// <summary>
	/// Creates a memo based on a user-supplied message.
	/// </summary>
	/// <param name="message">The user-supplied message.</param>
	/// <returns>The initialized memo.</returns>
	public static Memo FromMessage(string? message) => message is null ? NoMemo : new Memo() { Message = message };

	/// <inheritdoc cref="Zip302MemoFormat.EncodeNoMemo"/>
	public void Clear() => Zip302MemoFormat.EncodeNoMemo(this.bytes);

	/// <inheritdoc/>
	public readonly bool Equals(Memo other) => this.bytes[..].SequenceEqual(other.bytes);

	/// <inheritdoc/>
	public readonly override string ToString() => this.ToString(useQuotesAroundMessage: false);

	private readonly string ToString(bool useQuotesAroundMessage)
	{
		return this.MemoFormat switch
		{
			Zip302MemoFormat.MemoFormat.NoMemo => "(no memo)",
			Zip302MemoFormat.MemoFormat.Message => useQuotesAroundMessage ? $"\"{this.Message}\"" : this.Message!,
			Zip302MemoFormat.MemoFormat.ProprietaryData => $"Proprietary data: {Convert.ToHexString(this.ProprietaryData)}",
			_ => "(unrecognized format)",
		};
	}

	[InlineArray(512)]
	private struct Bytes512 : IEquatable<Bytes512>
	{
		private byte element0;

		/// <inheritdoc />
		readonly bool IEquatable<Bytes512>.Equals(Bytes512 other) => this[..].SequenceEqual(other);

		/// <inheritdoc cref="IEquatable{T}.Equals"/>
		public readonly bool Equals(in Bytes512 other) => this[..].SequenceEqual(other);
	}
}
