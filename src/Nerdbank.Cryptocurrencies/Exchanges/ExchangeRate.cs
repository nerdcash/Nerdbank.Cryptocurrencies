// Copyright (c) IronPigeon, LLC. All rights reserved.
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
	/// Gets the trading pair that this exchange rate represents.
	/// </summary>
	public TradingPair TradingPair => new(this.Basis.Security, this.TradeInterest.Security);

	/// <summary>
	/// Gets the cost of the <see cref="Basis"/> security for exactly one unit of <see cref="TradeInterest"/>.
	/// </summary>
	public SecurityAmount InBasisAmount => this.Basis.Security.Amount(this.Basis.Amount / this.TradeInterest.Amount);

	/// <summary>
	/// Gets the exchange rate with the <see cref="Basis"/> and <see cref="TradeInterest"/> swapped.
	/// </summary>
	public ExchangeRate OppositeDirection => new(this.TradeInterest, this.Basis);

	/// <summary>
	/// Gets the exchange rate with the <see cref="TradeInterest"/> amount normalized to 1,
	/// preparing the exchange rate to answer the question "How much of the <see cref="Basis"/> security
	/// does one unit of <see cref="TradeInterest"/> cost?".
	/// </summary>
	public readonly ExchangeRate Normalized
	{
		get
		{
			if (this.TradeInterest.Amount == 1m)
			{
				return this;
			}

			decimal factor = 1 / this.TradeInterest.Amount;
			return new(
				this.Basis.Security.Amount(this.Basis.Amount * factor),
				this.TradeInterest.Security.Amount(this.TradeInterest.Amount * factor));
		}
	}

	/// <summary>
	/// Converts a particular <see cref="SecurityAmount"/> to one that uses a different <see cref="Security"/> by applying a given exchange rate.
	/// </summary>
	/// <param name="amount">The base amount. This can be in either unit described by the <paramref name="exchangeRate"/>.</param>
	/// <param name="exchangeRate">The exchange rate between the security in <paramref name="amount"/> and one other.</param>
	/// <returns>The converted <see cref="SecurityAmount"/>.</returns>
	public static SecurityAmount operator *(SecurityAmount amount, ExchangeRate exchangeRate)
	{
		if (exchangeRate.Basis.Security == amount.Security)
		{
			return new(amount.Amount * exchangeRate.TradeInterest.Amount / exchangeRate.Basis.Amount, exchangeRate.TradeInterest.Security);
		}
		else if (exchangeRate.TradeInterest.Security == amount.Security)
		{
			return new(amount.Amount * exchangeRate.Basis.Amount / exchangeRate.TradeInterest.Amount, exchangeRate.Basis.Security);
		}
		else
		{
			throw new ArgumentException("The exchange rate has no security in common with the amount.");
		}
	}

	/// <summary>
	/// Compute an exchange rate using two others that share exactly one security in common.
	/// The common security will drop out of the result, leaving the two unique remaining securities
	/// in the computed exchange rate.
	/// </summary>
	/// <param name="left">The first exchange rate. The unique security in this value will be used as the basis of the returned exchange rate.</param>
	/// <param name="right">The second exchange rate. The unique security in this value will be used as the trade interest of the returned exchange rate.</param>
	/// <returns>An exchange rate made up of the unique securities in each operand.</returns>
	/// <exception cref="ArgumentException">Thrown if both arguments have the same pair of securities, or if they have no securities in common.</exception>
	public static ExchangeRate operator *(ExchangeRate left, ExchangeRate right)
	{
		// We want to arrange a unit multiplier situation to convert the unique security in left
		// to the unique security in right.
		// To do this, we'll arrange for the unique security on the left as the basis
		// and the unique security on the right as the trade interest.
		// We'll then multiply the two exchange rates together to get the result.
		// If the two securities are the same on both left and right, throw an error.
		if (left.Basis.Security == right.Basis.Security)
		{
			// Flip the right so the common security is the trade interest on the right.
			right = right.OppositeDirection;
		}
		else if (left.TradeInterest.Security == right.TradeInterest.Security)
		{
			// Flip the left so that the common security is the basis on the left.
			left = left.OppositeDirection;
		}
		else if (left.Basis.Security == right.TradeInterest.Security)
		{
			// This is already the way we want it.
		}
		else if (left.TradeInterest.Security == right.Basis.Security)
		{
			// It's exactly the opposite of what we want, so flipp both sides.
			(left, right) = (left.OppositeDirection, right.OppositeDirection);
		}
		else
		{
			throw new ArgumentException(Strings.ExchangeRatesRequireOneCommonSecurity);
		}

		// Check to see if what we expect to be the unique securities are also in common.
		if (left.TradeInterest.Security == right.Basis.Security)
		{
			throw new ArgumentException(Strings.ExchangeRatesRequireUniqueSecurities);
		}

		// Get the common security to also match in amount.
		decimal ratio = left.Basis.Amount / right.TradeInterest.Amount;
		right = new ExchangeRate(
			new SecurityAmount(right.Basis.Amount * ratio, right.Basis.Security),
			new SecurityAmount(right.TradeInterest.Amount * ratio, right.TradeInterest.Security));

		// We can now drop the common security from the result.
		return new ExchangeRate(left.TradeInterest, right.Basis);
	}

	/// <summary>
	/// Multiplies an exchange rate by some scalar.
	/// </summary>
	/// <param name="rate">The exchange rate to multiply.</param>
	/// <param name="factor">The factor to multiply the exchange rate by.</param>
	/// <returns>The result of multiplying the exchange rate by the factor.</returns>
	/// <remarks>
	/// This does <em>not</em> change the effectual exchange rate, but it <em>does</em> change its representation.
	/// For example a 2:1 exchange rate multiplied by 2 will result in a 4:2 exchange rate.
	/// </remarks>
	public static ExchangeRate operator *(ExchangeRate rate, decimal factor) => new(rate.Basis * factor, rate.TradeInterest * factor);

	/// <inheritdoc/>
	public override string ToString() => $"{this.Basis} <=> {this.TradeInterest}";
}
