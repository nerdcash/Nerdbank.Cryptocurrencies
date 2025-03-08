// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Cryptocurrencies.Exchanges;

/// <summary>
/// Describes a particular pairing of securities that may be traded on an exchange.
/// </summary>
public record struct TradingPair
{
	/// <summary>
	/// Initializes a new instance of the <see cref="TradingPair"/> struct.
	/// </summary>
	/// <param name="basis">The security offered in a trade.</param>
	/// <param name="tradeInterest">The security received in a trade.</param>
	public TradingPair(Security basis, Security tradeInterest)
	{
		if (SecuritySymbolOnlyComparer.Instance.Equals(basis, tradeInterest))
		{
			throw new ArgumentException(Strings.TradingPairRequiresUniqueSecurities);
		}

		this.Basis = basis;
		this.TradeInterest = tradeInterest;
	}

	/// <summary>
	/// Gets the trading pair, expressed such that the <see cref="TradeInterest"/> becomes the <see cref="Basis"/> and vice versa.
	/// </summary>
	/// <returns>The inverted trade.</returns>
	public TradingPair OppositeDirection => new TradingPair(this.TradeInterest, this.Basis);

	/// <summary>
	/// Gets the security offered in a trade.
	/// </summary>
	public Security Basis { get; }

	/// <summary>
	/// Gets the security received in a trade.
	/// </summary>
	public Security TradeInterest { get; }

	/// <inheritdoc/>
	public override string ToString() => $"{this.TradeInterest.TickerSymbol} <=> {this.Basis.TickerSymbol}";

	/// <summary>
	/// Tests whether a given pair of securities matches this trading pair, without regard to basis and trade interest direction.
	/// </summary>
	/// <param name="one">One security.</param>
	/// <param name="two">Another security.</param>
	/// <returns><see langword="true"/> if and only if the two provided securities are equal to the two securities of this trading pair.</returns>
	/// <remarks>
	/// Security equality is determined by <see cref="SecuritySymbolOnlyComparer.Instance"/>.
	/// </remarks>
	internal bool IsMatchingPair(Security one, Security two)
	{
		return (SecuritySymbolOnlyComparer.Instance.Equals(this.Basis, one) && SecuritySymbolOnlyComparer.Instance.Equals(this.TradeInterest, two))
			|| (SecuritySymbolOnlyComparer.Instance.Equals(this.Basis, two) && SecuritySymbolOnlyComparer.Instance.Equals(this.TradeInterest, one));
	}
}
