// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Cryptocurrencies.Exchanges;
using Nerdbank.Zcash.App;

public class SecurityAmountFormattedTests
{
	[Fact]
	public void Units()
	{
		SecurityAmountFormatted z = Create(1.23m, ZcashNetwork.MainNet);
		Assert.Equal("ZEC", z.Units);
		SecurityAmountFormatted t = Create(1.23m, ZcashNetwork.TestNet);
		Assert.Equal("TAZ", t.Units);
	}

	[Fact]
	public void DarkText()
	{
		Assert.Equal("1,000.", Create(1_000m).DarkText);
		Assert.Equal("1.", Create(1m).DarkText);
		Assert.Equal("1.23", Create(1.23m).DarkText);
		Assert.Equal("1.12345678", Create(1.12345678m).DarkText);
		Assert.Equal("12,345.12345678", Create(12345.12345678m).DarkText);
	}

	[Fact]
	public void LightText()
	{
		Assert.Equal("00000000", Create(1m).LightText);
		Assert.Equal("000000", Create(1.23m).LightText);
		Assert.Equal(string.Empty, Create(1.12345678m).LightText);
	}

	private static SecurityAmountFormatted Create(decimal amount, ZcashNetwork network = ZcashNetwork.MainNet) => new SecurityAmountFormatted(new SecurityAmount(amount, network.AsSecurity()));
}
