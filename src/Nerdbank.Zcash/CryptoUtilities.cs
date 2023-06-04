// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

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

	/// <summary>
	/// Copies the contents of one buffer to another,
	/// after verifying that the lengths of the two buffers are equal.
	/// </summary>
	/// <param name="source">The source buffer.</param>
	/// <param name="destination">The target buffer.</param>
	/// <param name="parameterName">Omit this optional parameter, or specify the name of the parameter whose argument is passed in as the <paramref name="source"/>.</param>
	/// <exception cref="ArgumentException">
	/// Thrown when the length of the <paramref name="source"/> and <paramref name="destination"/> spans do not equal.
	/// In the exception message the length of the <paramref name="destination"/> buffer will be described as the expected length.
	/// </exception>
	internal static void CopyToWithLengthCheck(this ReadOnlySpan<byte> source, Span<byte> destination, [CallerArgumentExpression(nameof(source))] string? parameterName = null)
	{
		if (source.Length != destination.Length)
		{
			throw new ArgumentException(Strings.FormatUnexpectedLength(destination.Length, source.Length), parameterName);
		}

		source.CopyTo(destination);
	}
}
