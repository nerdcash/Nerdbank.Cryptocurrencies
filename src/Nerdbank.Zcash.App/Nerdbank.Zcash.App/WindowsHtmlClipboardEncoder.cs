// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;
using System.Globalization;
using System.Text;
using Avalonia.Input;

namespace Nerdbank.Zcash.App;

/// <summary>
/// Encodes HTML content for the clipboard.
/// </summary>
/// <remarks>
/// On Windows, this prefixes a header and encodes the resulting string as UTF-8.
/// </remarks>
public static class WindowsHtmlClipboardEncoder
{
	/// <summary>
	/// The data format name for HTML on the clipboard.
	/// </summary>
	/// <remarks>
	/// The format name is documented for <see href="https://learn.microsoft.com/en-us/windows/win32/dataxchg/html-clipboard-format">Windows</see>.
	/// </remarks>
	public const string HtmlClipboardFormatName = "HTML Format";

	private static readonly Encoding CharEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

	/// <summary>
	/// Adds an HTML fragment to a <see cref="DataObject"/>.
	/// </summary>
	/// <param name="dataObject">The data object.</param>
	/// <param name="fragment">The HTML fragment.</param>
	public static void SetHtml(this DataObject dataObject, string fragment)
	{
		dataObject.Set(HtmlClipboardFormatName, Encode("<html><body>", fragment, "</body></html>"));
	}

	/// <summary>
	/// Encodes the <see href="https://learn.microsoft.com/en-us/windows/win32/dataxchg/html-clipboard-format"><c>CF_HTML</c> format</see> for the clipboard.
	/// </summary>
	/// <param name="leading">The HTML that precedes the selected text to be copied.</param>
	/// <param name="content">The mid-document fragment to include in a subsequent paste.</param>
	/// <param name="trailing">The HTML that follows the selected text to be copied.</param>
	/// <returns>The raw data to add to the clipboard.</returns>
	public static byte[] Encode(string leading, string content, string trailing)
	{
		int htmlLength = leading.Length + content.Length + trailing.Length;
		int htmlByteMaxLength = CharEncoding.GetMaxByteCount(htmlLength);
		int clipboardHeaderMaxLength = 100;

		byte[] buffer = ArrayPool<byte>.Shared.Rent(clipboardHeaderMaxLength + htmlByteMaxLength);
		try
		{
			Span<byte> unwrittenArea = buffer;

			WriteHeader(ref unwrittenArea, "Version"u8, "1.0"u8);
			Span<byte> startHtmlIndex = WriteHeaderPlaceholder(ref unwrittenArea, "StartHTML"u8);
			Span<byte> endHtmlIndex = WriteHeaderPlaceholder(ref unwrittenArea, "EndHTML"u8);
			Span<byte> startFragmentIndex = WriteHeaderPlaceholder(ref unwrittenArea, "StartFragment"u8);
			Span<byte> endFragmentIndex = WriteHeaderPlaceholder(ref unwrittenArea, "EndFragment"u8);

			ReadOnlySpan<byte> header = buffer[..unwrittenArea.Length];

			RecordPosition(startHtmlIndex, unwrittenArea);
			Encode(ref unwrittenArea, leading);

			Write(ref unwrittenArea, "<!--StartFragment-->"u8);
			RecordPosition(startFragmentIndex, unwrittenArea);
			Encode(ref unwrittenArea, content);
			RecordPosition(endFragmentIndex, unwrittenArea);
			Write(ref unwrittenArea, "<!--EndFragment-->"u8);

			Encode(ref unwrittenArea, trailing);
			RecordPosition(endHtmlIndex, unwrittenArea);

			return buffer[..(buffer.Length - unwrittenArea.Length)].ToArray();
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(buffer);
		}

		int FormatInteger(Span<byte> buffer, int value)
		{
			Assumes.True(value.TryFormat(buffer, out int bytesWritten, "D10", CultureInfo.InvariantCulture));
			return bytesWritten;
		}

		int RecordPosition(Span<byte> indexPlaceholder, ReadOnlySpan<byte> unwrittenArea)
		{
			int position = buffer.Length - unwrittenArea.Length;
			return FormatInteger(indexPlaceholder, position);
		}

		Span<byte> Encode(ref Span<byte> target, ReadOnlySpan<char> text)
		{
			int bytesWritten = CharEncoding.GetBytes(text, target);
			Span<byte> written = target[..bytesWritten];
			target = target[bytesWritten..];
			return written;
		}

		Span<byte> Write(ref Span<byte> target, ReadOnlySpan<byte> content)
		{
			Span<byte> written = target[..content.Length];
			content.CopyTo(written);
			target = target[content.Length..];
			return written;
		}

		Span<byte> WriteHeader(ref Span<byte> buffer, ReadOnlySpan<byte> name, ReadOnlySpan<byte> value)
		{
			Write(ref buffer, name);
			Write(ref buffer, ":"u8);
			Span<byte> valueSpan = Write(ref buffer, value);
			Write(ref buffer, "\r\n"u8);
			return valueSpan;
		}

		Span<byte> WriteHeaderPlaceholder(ref Span<byte> buffer, ReadOnlySpan<byte> name)
		{
			Write(ref buffer, name);
			Write(ref buffer, ":"u8);
			int bytesWritten = FormatInteger(buffer, 0);
			Span<byte> written = buffer[..bytesWritten];
			buffer = buffer[bytesWritten..];
			Write(ref buffer, "\r\n"u8);
			return written;
		}
	}
}
