// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash;

/// <summary>
/// An interface implemented by elements that may be used in a unified address or viewing key.
/// </summary>
internal interface IUnifiedEncodingElement
{
	/// <summary>
	/// Gets the type code that is used when this key is included in a unified viewing key.
	/// </summary>
	byte UnifiedTypeCode { get; }

	/// <summary>
	/// Gets the length of this element in its unified serialized form.
	/// </summary>
	int UnifiedDataLength { get; }

	/// <summary>
	/// Serializes the element to a buffer for inclusion in a unified.
	/// </summary>
	/// <param name="destination">The buffer to write to. It should be at least as large as indicated by <see cref="UnifiedDataLength"/>.</param>
	/// <returns>The number of bytes written to <paramref name="destination" />.</returns>
	int WriteUnifiedData(Span<byte> destination);
}
