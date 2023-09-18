// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
#if ZCASH
using Nerdbank.Zcash;
#elif BITCOIN
using Nerdbank.Bitcoin;
#endif

namespace Nerdbank.Cryptocurrencies;

/// <summary>
/// Common crypto functions needed by multiple assemblies but should not be made publicly accessible.
/// </summary>
internal static class SharedCryptoUtilities
{
	/// <summary>
	/// Copies the contents of one buffer to another,
	/// after verifying that the lengths of the two buffers are equal.
	/// </summary>
	/// <param name="source">The source buffer.</param>
	/// <param name="destination">The target buffer.</param>
	/// <param name="parameterName">Omit this optional parameter, or specify the name of the parameter whose argument is passed in as the <paramref name="source"/>.</param>
	/// <param name="allowShorterInput"><see langword="true" /> to allow for less bytes as input.</param>
	/// <exception cref="ArgumentException">
	/// Thrown when the length of the <paramref name="source"/> and <paramref name="destination"/> spans do not equal.
	/// In the exception message the length of the <paramref name="destination"/> buffer will be described as the expected length.
	/// </exception>
	internal static void CopyToWithLengthCheck(this ReadOnlySpan<byte> source, Span<byte> destination, [CallerArgumentExpression(nameof(source))] string? parameterName = null, bool allowShorterInput = false)
	{
		if (!(source.Length == destination.Length || (allowShorterInput && source.Length < destination.Length)))
		{
			throw new ArgumentException(SharedStrings.FormatUnexpectedLength(destination.Length, source.Length), parameterName);
		}

		source.CopyTo(destination);

		if (source.Length < destination.Length)
		{
			// Ensure the slack space in the target area is cleared.
			destination[source.Length..].Clear();
		}
	}

	/// <inheritdoc cref="ReadOnlySpan{T}.CopyTo(Span{T})"/>
	/// <returns>The number of elements copied.</returns>
	internal static int CopyToRetLength<T>(this ReadOnlySpan<T> source, Span<T> destination)
	{
		source.CopyTo(destination);
		return source.Length;
	}
}
