// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash;

/// <summary>
/// An interface implemented by viewing keys that may be used in a unified viewing key.
/// </summary>
internal interface IUnifiedEncodableViewingKey : IViewingKey
{
	/// <summary>
	/// Gets the type code that is used when this key is included in a unified viewing key.
	/// </summary>
	byte UnifiedTypeCode { get; }

	/// <summary>
	/// Gets the length of this viewing key in its unified viewing key serialized form.
	/// </summary>
	int UnifiedKeyContributionLength { get; }

	/// <summary>
	/// Serializes the viewing key to a buffer for inclusion in a unified viewing key.
	/// </summary>
	/// <param name="destination">The buffer to write to. It should be at least as large as indicated by <see cref="UnifiedKeyContributionLength"/>.</param>
	/// <returns>The number of bytes written to <paramref name="destination" />.</returns>
	int WriteUnifiedViewingKeyContribution(Span<byte> destination);
}
