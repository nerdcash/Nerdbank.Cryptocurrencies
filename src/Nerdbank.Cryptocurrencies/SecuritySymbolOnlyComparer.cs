// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Cryptocurrencies;

/// <summary>
/// Tests equality between <see cref="Security"/> values based only on their <see cref="Security.TickerSymbol"/> property.
/// </summary>
internal class SecuritySymbolOnlyComparer : IEqualityComparer<Security>
{
	/// <summary>
	/// Gets the singleton instance.
	/// </summary>
	public static readonly SecuritySymbolOnlyComparer Instance = new();

	private SecuritySymbolOnlyComparer()
	{
	}

	/// <inheritdoc/>
	public bool Equals(Security? x, Security? y) => x?.TickerSymbol.Equals(y?.TickerSymbol, StringComparison.OrdinalIgnoreCase) ?? false;

	/// <inheritdoc/>
	public int GetHashCode(Security obj) => obj.TickerSymbol.GetHashCode(StringComparison.OrdinalIgnoreCase);
}
