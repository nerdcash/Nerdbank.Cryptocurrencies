// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash;

/// <summary>
/// An optional interface for <see cref="IUnifiedEncodingElement"/> implementations
/// that can be used when checking whether their compound container contains equal elements.
/// </summary>
internal interface IUnifiedEncodingElementEqualityComparer
{
	/// <summary>
	/// Compares equality between two unified encoding elements.
	/// </summary>
	/// <param name="other">The counterpart element to compare with.</param>
	/// <returns>A value indicating whether the two elements are equal.</returns>
	bool Equals(IUnifiedEncodingElementEqualityComparer? other);

	/// <summary>
	/// Gets a special hash code that only considers elements that the <see cref="Equals"/> method on this interface considers.
	/// </summary>
	/// <returns>A hash code.</returns>
	int GetHashCode();
}
