// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public class AccountBalancesTests
{
	[Test]
	public void ViewedNotesArePartOfTheMainBalance()
	{
		AccountBalances account = new()
		{
			Spendable = Security.ZEC.Amount(1m),
			ImmatureChange = Security.ZEC.Amount(0.2m),
			MinimumFees = Security.ZEC.Amount(-0.0001m),
			ImmatureIncome = Security.ZEC.Amount(0.3m),
			Dust = Security.ZEC.Amount(0.00005m),
			Incoming = Security.ZEC.Amount(0.4m),
			IncomingDust = Security.ZEC.Amount(0.00001m),
			Viewed = Security.ZEC.Amount(0.5m),
		};

		Assert.Equal(0.5m, account.Viewed.Amount);
		Assert.Equal(
			account.Spendable + account.MinimumFees + account.ImmatureChange + account.ImmatureIncome + account.Viewed,
			account.MainBalance);
	}
}
