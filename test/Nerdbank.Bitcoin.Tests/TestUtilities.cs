// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

internal static class TestUtilities
{
	internal static string Base58ToHex(string base58)
	{
		int maxLength = Base58Check.GetMaxDecodedLength(base58.Length);
		Span<byte> bytes = stackalloc byte[maxLength];
		int length = Base58Check.Decode(base58, bytes);
		return Convert.ToHexString(bytes[..length]);
	}
}
