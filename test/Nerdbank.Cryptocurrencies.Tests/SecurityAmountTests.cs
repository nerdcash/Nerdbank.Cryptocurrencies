// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
	public void Multiply()
	{
		Assert.Equal(Security.USD.Amount(15), Security.USD.Amount(5) * 3);
		Assert.Equal(Security.USD.Amount(15), 3 * Security.USD.Amount(5));
	}

	[Fact]
	public void Divide()
	{
		Assert.Equal(Security.USD.Amount(15), Security.USD.Amount(45) / 3);
	}

	[Fact]
	public void GreaterThan()
	{
		var smaller = new SecurityAmount(1.0m, Security.USD);
		var larger = new SecurityAmount(2.0m, Security.USD);

		Assert.True(larger > smaller);
		Assert.True(larger >= smaller);
		Assert.False(smaller > larger);
		Assert.False(smaller >= larger);
	}

	[Fact]
	public void GreaterThan_Equal()
	{
		var amount1 = new SecurityAmount(1.0m, Security.USD);
		var amount2 = new SecurityAmount(1.0m, Security.USD);

		Assert.False(amount1 > amount2);
		Assert.True(amount1 >= amount2);
	}

	[Fact]
	public void GreaterThan_DifferentSecurities()
	{
		var amount1 = new SecurityAmount(1.0m, Security.USD);
		var amount2 = new SecurityAmount(1.0m, Security.BTC);

		Assert.Throws<ArgumentException>(() => amount1 > amount2);
	}

	[Fact]
	public void GreaterThanOrEqual_DifferentSecurities()
	{
		var amount1 = new SecurityAmount(1.0m, Security.USD);
		var amount2 = new SecurityAmount(1.0m, Security.BTC);

		Assert.Throws<ArgumentException>(() => amount1 >= amount2);
	}

	[Fact]
	public void LessThan()
	{
		var smaller = new SecurityAmount(1.0m, Security.USD);
		var larger = new SecurityAmount(2.0m, Security.USD);

		Assert.True(smaller < larger);
		Assert.True(smaller <= larger);
		Assert.False(larger < smaller);
		Assert.False(larger <= smaller);
	}

	[Fact]
	public void LessThan_Equal()
	{
		var amount1 = new SecurityAmount(1.0m, Security.USD);
		var amount2 = new SecurityAmount(1.0m, Security.USD);

		Assert.False(amount1 < amount2);
		Assert.True(amount1 <= amount2);
	}

	[Fact]
	public void LessThan_DifferentSecurities()
	{
		var amount1 = new SecurityAmount(1.0m, Security.USD);
		var amount2 = new SecurityAmount(1.0m, Security.BTC);

		Assert.Throws<ArgumentException>(() => amount1 < amount2);
	}

	[Fact]
	public void LessThanOrEqual_DifferentSecurities()
	{
		var amount1 = new SecurityAmount(1.0m, Security.USD);
		var amount2 = new SecurityAmount(1.0m, Security.BTC);

		Assert.Throws<ArgumentException>(() => amount1 <= amount2);
	}

	[Fact]
	public void RoundedAmount()
	{
		Assert.Equal(1.20m, Security.USD.Amount(1.201m).RoundedAmount);
		Assert.Equal(1.20m, Security.USD.Amount(1.199m).RoundedAmount);
	}

	[Fact]
	public void RoundedAmount_Default()
	{
		Assert.Equal(0, default(SecurityAmount).RoundedAmount);
	}

	[Fact]
	public void ToString_Formatting()
	{
		Assert.Equal("1.20000000 ZEC", new SecurityAmount(1.2m, Security.ZEC).ToString());
	}
}
