// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Cryptocurrencies.Exchanges;

/// <summary>
/// Represents a value equivalency between two securities.
/// </summary>
/// <param name="Basis">The amount that must be paid to obtain the security identified in <paramref name="TradeInterest"/>.</param>
/// <param name="TradeInterest">The amount of a security received in exchange for providing <paramref name="Basis"/>.</param>
public record struct ExchangeRate(SecurityAmount Basis, SecurityAmount TradeInterest)
{
	/// <summary>
	/// Gets the cost of the <see cref="Basis"/> security for exactly one unit of <see cref="TradeInterest"/>.
	/// </summary>
	public SecurityAmount InBasisAmount => this.Basis.Security.Amount(this.Basis.Amount / this.TradeInterest.Amount);

	/// <summary>
	/// Gets the exchange rate with the <see cref="Basis"/> and <see cref="TradeInterest"/> swapped.
	/// </summary>
	public ExchangeRate OppositeDirection => new(this.TradeInterest, this.Basis);

	/// <inheritdoc/>
	public override string ToString() => $"{this.Basis} <=> {this.TradeInterest}";
}
