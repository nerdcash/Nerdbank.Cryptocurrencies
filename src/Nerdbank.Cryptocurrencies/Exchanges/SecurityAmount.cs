// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Cryptocurrencies.Exchanges;

/// <summary>
/// Describes an amount of a particular security.
/// </summary>
/// <param name="Amount">The amount of some security.</param>
/// <param name="Security">The security being counted.</param>
public record struct SecurityAmount(decimal Amount, Security Security)
{
	/// <summary>
	/// Adds the amounts of the same security.
	/// </summary>
	/// <param name="left">The left operand.</param>
	/// <param name="right">The right operand.</param>
	/// <returns>The sum of two amounts.</returns>
	/// <exception cref="ArgumentException">Thrown when the two amounts are of different securities.</exception>
	public static SecurityAmount operator +(SecurityAmount left, SecurityAmount right)
	{
		if (left.Amount == 0)
		{
			return right;
		}

		if (right.Amount == 0)
		{
			return left;
		}

		if (left.Security != right.Security)
		{
			throw new ArgumentException(Strings.SecurityMismatch);
		}

		return new SecurityAmount(left.Amount + right.Amount, left.Security);
	}

	/// <summary>
	/// Subtracts one amount of a security from another.
	/// </summary>
	/// <param name="left">The original amount.</param>
	/// <param name="right">The amount to subtract.</param>
	/// <returns>The difference between the two amounts.</returns>
	/// <exception cref="ArgumentException">Thrown when the two amounts are of different securities.</exception>
	public static SecurityAmount operator -(SecurityAmount left, SecurityAmount right) => left + -right;

	/// <summary>
	/// Negates the amount of some security.
	/// </summary>
	/// <param name="right">The amount to negate.</param>
	/// <returns>The negated amount.</returns>
	public static SecurityAmount operator -(SecurityAmount right) => new(-right.Amount, right.Security);

	/// <inheritdoc/>
	public override string ToString() => this.Security is not null ? $"{this.Amount.ToString("F" + this.Security.Precision)} {this.Security.TickerSymbol}" : "0";
}
