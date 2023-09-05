// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;

namespace Nerdbank.Zcash;

/// <summary>
/// Provides standardize construction and parsing of memo fields.
/// </summary>
/// <remarks>
/// This is an implementation of <see href="https://zips.z.cash/zip-0302">ZIP-302: Standardized Memo Field Format</see>.
/// </remarks>
public static class Zip302MemoFormat
{
	private static readonly UTF8Encoding TextEncoding = new(encoderShouldEmitUTF8Identifier: false);

	/// <summary>
	/// Enumerates the ZIP-302 standardized memo formats.
	/// </summary>
	public enum MemoFormat
	{
		/// <summary>
		/// The memo is explicitly empty. No memo has been provided.
		/// </summary>
		NoMemo,

		/// <summary>
		/// A human-readable string (which may be empty) is present in the memo field.
		/// </summary>
		/// <remarks>
		/// Use <see cref="TryDecodeMessage"/> to retrieve the content.
		/// </remarks>
		Message,

		/// <summary>
		/// Proprietary data is in the memo field.
		/// </summary>
		/// <remarks>
		/// Use <see cref="TryDecodeProprietaryData"/> to retrieve the content.
		/// </remarks>
		ProprietaryData,

		/// <summary>
		/// This memo is structured according to a standard written after this implementation was written.
		/// </summary>
		Reserved,
	}

	/// <summary>
	/// Encodes a human-readable message as UTF-8 bytes.
	/// </summary>
	/// <param name="text">The human-readable message.</param>
	/// <param name="memo">The 512-byte buffer that serves as the memo. This will be fully initialized.</param>
	public static void EncodeMessage(ReadOnlySpan<char> text, Span<byte> memo)
	{
		ThrowIfUnexpectedMemoSize(memo);

		int bytesWritten;
		try
		{
			bytesWritten = TextEncoding.GetBytes(text, memo);
		}
		catch (ArgumentException)
		{
			if (TextEncoding.GetByteCount(text) > memo.Length)
			{
				throw new ArgumentException(Strings.MemoDataTooLarge, nameof(text));
			}

			throw;
		}

		memo[bytesWritten..].Clear();
	}

	/// <summary>
	/// Encodes explicitly that there is no memo in the memo field.
	/// </summary>
	/// <param name="memo">The 512-byte buffer that serves as the memo. This will be fully initialized.</param>
	public static void EncodeNoMemo(Span<byte> memo)
	{
		ThrowIfUnexpectedMemoSize(memo);

		memo[0] = 0xF6;
		memo[1..].Clear();
	}

	/// <summary>
	/// Encodes proprietary data in the memo field.
	/// </summary>
	/// <param name="data">The private data, which may be up to 511 bytes.</param>
	/// <param name="memo">The 512-byte buffer that serves as the memo. This will be fully initialized.</param>
	/// <remarks>
	/// Proprietary data should somehow be marked for recognizeability by your wallet so your wallet can read it,
	/// while other wallets that may contain their own proprietary format will recognize that this is <em>not</em> their format.
	/// </remarks>
	public static void EncodeProprietaryData(ReadOnlySpan<byte> data, Span<byte> memo)
	{
		ThrowIfUnexpectedMemoSize(memo);
		Requires.Argument(data.Length <= 511, nameof(data), Strings.FormatLengthOutsideExpectedRange(0, 511, data.Length));

		memo[0] = 0xFF;
		data.CopyTo(memo[1..]);
	}

	/// <summary>
	/// Decodes the human readable message from a memo, if there is one.
	/// </summary>
	/// <param name="memo">The memo field. This must be exactly 512 bytes in length.</param>
	/// <param name="text">Receives the message, if the memo format indicates a human readable message.</param>
	/// <returns><see langword="true" /> if the memo contains a human-readable message (which may be empty); <see langword="false" /> otherwise.</returns>
	public static bool TryDecodeMessage(ReadOnlySpan<byte> memo, [NotNullWhen(true)] out string? text)
	{
		ThrowIfUnexpectedMemoSize(memo);

		if (memo[0] > 0xF4)
		{
			text = null;
			return false;
		}

		ReadOnlySpan<byte> trimmedMemo = memo.TrimEnd((byte)0);
		text = trimmedMemo.IsEmpty ? string.Empty : TextEncoding.GetString(trimmedMemo);

		return true;
	}

	/// <summary>
	/// Decodes proprietary data from the memo, if there is any.
	/// </summary>
	/// <param name="memo">The memo field. This must be exactly 512 bytes in length.</param>
	/// <param name="data">Receives the proprietary data, if there is any. Must be exactly 511 bytes in length.</param>
	/// <returns><see langword="true" /> if the memo contained proprietary data; <see langword="false" /> otherwise.</returns>
	public static bool TryDecodeProprietaryData(ReadOnlySpan<byte> memo, Span<byte> data)
	{
		ThrowIfUnexpectedMemoSize(memo);
		Requires.Argument(data.Length == 511, nameof(data), SharedStrings.FormatUnexpectedLength(511, data.Length));

		if (memo[0] != 0xFF)
		{
			return false;
		}

		memo[1..].CopyTo(data);
		return true;
	}

	/// <summary>
	/// Detects the format of the memo field, assuming conformance to the ZIP-302 specification.
	/// </summary>
	/// <param name="memo">The 512-byte memo field.</param>
	/// <returns>The memo format used.</returns>
	public static MemoFormat DetectMemoFormat(ReadOnlySpan<byte> memo)
	{
		ThrowIfUnexpectedMemoSize(memo);

		return memo[0] switch
		{
			<= 0xF4 => MemoFormat.Message,
			0xFF => MemoFormat.ProprietaryData,
			0xF5 or (>= 0xF7 and <= 0xFE) => MemoFormat.Reserved,
			0xF6 => memo[1..].SequenceEqual(stackalloc byte[511]) ? MemoFormat.NoMemo : MemoFormat.Reserved,
		};
	}

	private static void ThrowIfUnexpectedMemoSize(ReadOnlySpan<byte> memo)
	{
		Requires.Argument(memo.Length == 512, nameof(memo), SharedStrings.FormatUnexpectedLength(512, memo.Length));
	}
}
