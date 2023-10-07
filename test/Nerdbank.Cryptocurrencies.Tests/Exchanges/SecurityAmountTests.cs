// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Cryptocurrencies.Exchanges;

public class SecurityAmountTests
{
	[Fact]
	public void Add_SameSecurity()
	{
		SecurityAmount expected = new(1.5m, Security.DASH);
		SecurityAmount actual = new SecurityAmount(1.2m, Security.DASH) + new SecurityAmount(0.3m, Security.DASH);
		Assert.Equal(expected, actual);
	}

	[Fact]
	public void Add_IncompatibleSecurity()
	{
		Assert.Throws<ArgumentException>(() => new SecurityAmount(1.2m, Security.DASH) + new SecurityAmount(0.3m, Security.BTC));
	}

	[Fact]
	public void Subtract_SameSecurity()
	{
		SecurityAmount expected = new(0.9m, Security.DASH);
		SecurityAmount actual = new SecurityAmount(1.2m, Security.DASH) - new SecurityAmount(0.3m, Security.DASH);
		Assert.Equal(expected, actual);
	}

	[Fact]
	public void Subtract_IncompatibleSecurity()
	{
		Assert.Throws<ArgumentException>(() => new SecurityAmount(1.2m, Security.DASH) - new SecurityAmount(0.3m, Security.BTC));
	}

	[Fact]
	public void Negate()
	{
		Assert.Equal(new SecurityAmount(-5, Security.DASH), -new SecurityAmount(5, Security.DASH));
		Assert.Equal(new SecurityAmount(5, Security.DASH), -new SecurityAmount(-5, Security.DASH));
	}

	[Fact]
	public void ToString_Formatting()
	{
		Assert.Equal("1.20000000 ZEC", new SecurityAmount(1.2m, Security.ZEC).ToString());
	}
}
