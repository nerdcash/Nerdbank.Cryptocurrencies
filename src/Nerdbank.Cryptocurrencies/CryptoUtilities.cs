// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Cryptocurrencies;

/// <summary>
/// Internal utilities for this library.
/// </summary>
internal static class CryptoUtilities
{
	/// <inheritdoc cref="ReadOnlySpan{T}.CopyTo(Span{T})"/>
	/// <returns>The number of elements copied.</returns>
	internal static int CopyToRetLength<T>(this ReadOnlySpan<T> source, Span<T> destination)
	{
		source.CopyTo(destination);
		return source.Length;
	}
}
