// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public class ZcashAmountFormattedTests
{
	[Fact]
	public void Units()
	{
		ZcashAmountFormatted z = new(1.23m);
		Assert.Equal("ZEC", z.Units);
		ZcashAmountFormatted t = new(1.23m) { Network = ZcashNetwork.TestNet };
		Assert.Equal("TAZ", t.Units);
	}

	[Fact]
	public void DarkText()
	{
		Assert.Equal("1,000.", new ZcashAmountFormatted(1_000m).DarkText);
		Assert.Equal("1.", new ZcashAmountFormatted(1m).DarkText);
		Assert.Equal("1.23", new ZcashAmountFormatted(1.23m).DarkText);
		Assert.Equal("1.12345678", new ZcashAmountFormatted(1.12345678m).DarkText);
		Assert.Equal("12,345.12345678", new ZcashAmountFormatted(12345.12345678m).DarkText);
	}

	[Fact]
	public void LightText()
	{
		Assert.Equal("00000000", new ZcashAmountFormatted(1m).LightText);
		Assert.Equal("000000", new ZcashAmountFormatted(1.23m).LightText);
		Assert.Equal(string.Empty, new ZcashAmountFormatted(1.12345678m).LightText);
	}
}
