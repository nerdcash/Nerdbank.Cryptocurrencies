// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Cryptocurrencies.Exchanges;

public class SecurityTests
{
	[Fact]
	public void WellKnown()
	{
		Assert.Same(Security.ATOM, Security.WellKnown["atom"]);
		Assert.Same(Security.ATOM, Security.WellKnown["ATOM"]);
	}

	[Fact]
	public void Amount()
	{
		SecurityAmount expected = new(1.2m, Security.ZEC);
		SecurityAmount actual = Security.ZEC.Amount(1.2m);
		Assert.Equal(expected, actual);
	}

	[Fact]
	public void ZEC_Precision() => Assert.Equal(8, Security.ZEC.Precision);
}
