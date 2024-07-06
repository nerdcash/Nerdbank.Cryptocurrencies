// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace Nerdbank.Zcash.App;

public static class NativeMethods
{
	private const string LibraryName = "nerdbank_zcash_rust";

	public static unsafe uint DecodeQrCodeFromFile(string filePath, Span<char> decoded)
	{
		fixed (char* filePathPtr = filePath)
		{
			fixed (char* decodedPtr = decoded)
			{
				int result = decode_qr_code_from_file(filePathPtr, (nuint)filePath.Length, decodedPtr, (nuint)decoded.Length);
				return result >= 0 ? checked((uint)result) : throw new InvalidOperationException("Failed to decode QR code.");
			}
		}
	}

	public static unsafe uint DecodeQrCodeFromImage(ReadOnlySpan<byte> image, Span<char> decoded)
	{
		fixed (byte* imagePtr = image)
		{
			fixed (char* decodedPtr = decoded)
			{
				int result = decode_qr_code_from_image(imagePtr, (nuint)image.Length, decodedPtr, (nuint)decoded.Length);
				return result >= 0 ? checked((uint)result) : throw new InvalidOperationException("Failed to decode QR code.");
			}
		}
	}

	[DllImport(LibraryName)]
	private static unsafe extern int decode_qr_code_from_file(char* file_path, nuint file_path_length, char* decoded, nuint decoded_length);

	[DllImport(LibraryName)]
	private static unsafe extern int decode_qr_code_from_image(byte* image, nuint image_length, char* decoded, nuint decoded_length);
}
