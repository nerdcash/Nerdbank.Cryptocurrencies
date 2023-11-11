// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Cryptocurrencies.Exchanges;

namespace Nerdbank.Zcash.App;

public class SecurityAmountFormatted
{
	[Obsolete("For XAML use only.", error: true)]
	public SecurityAmountFormatted()
		: this(new(1.23m, Security.ZEC))
	{
	}

	public SecurityAmountFormatted(SecurityAmount securityAmount)
	{
		this.Amount = securityAmount;
		string fullValue = securityAmount.Amount.ToString("N" + (securityAmount.Security?.Precision ?? 0));
		this.DarkText = fullValue.TrimEnd('0');
		this.LightText = fullValue.Substring(this.DarkText.Length);
	}

	public SecurityAmount Amount { get; }

	public string DarkText { get; }

	public string LightText { get; }

	public string Units => this.Amount.Security?.TickerSymbol ?? string.Empty;
}
