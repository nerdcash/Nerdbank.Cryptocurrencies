// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash;

/// <summary>
/// Internal utilities for this library.
/// </summary>
internal static class CryptoUtilities
{
	/// <summary>
	/// Converts a .NET <see cref="System.Numerics.BigInteger"/> to the BouncyCastle equivalent.
	/// </summary>
	/// <param name="value">The big integer to convert.</param>
	/// <returns>The BouncyCastle representation of the big integer.</returns>
	internal static Org.BouncyCastle.Math.BigInteger ToBouncyCastle(this System.Numerics.BigInteger value)
	{
		Span<byte> bytes = stackalloc byte[(int)(value.GetBitLength() / 8) + 1];
		Assumes.True(value.TryWriteBytes(bytes, out int bytesWritten, isUnsigned: true, isBigEndian: true));
		return new(value.Sign, bytes);
	}

	/// <summary>
	/// Converts a BouncyCastle <see cref="Org.BouncyCastle.Math.BigInteger"/> to its .NET Numerics equivalent.
	/// </summary>
	/// <param name="value">The big integer to convert.</param>
	/// <returns>The .NET representation of the bg integer.</returns>
	internal static System.Numerics.BigInteger ToNumerics(this Org.BouncyCastle.Math.BigInteger value)
	{
		Span<byte> bytes = stackalloc byte[value.GetLengthofByteArrayUnsigned()];
		value.ToByteArray(bytes);
		return new(bytes, isUnsigned: false, isBigEndian: true);
	}
}
