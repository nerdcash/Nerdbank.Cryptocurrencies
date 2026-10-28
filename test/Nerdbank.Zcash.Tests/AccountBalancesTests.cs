// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using uniffi.LightWallet;

public class AccountBalancesTests
{
	[Test]
	public void ViewedNotesRoundTripAndAppearInTheMainBalance()
	{
		UserBalances balances = new(
			spendable: 100_000_000,
			immatureChange: 20_000_000,
			minimumFees: 10_000,
			immatureIncome: 30_000_000,
			dust: 5_000,
			incoming: 40_000_000,
			incomingDust: 1_000,
			viewed: 50_000_000);

		FfiConverterTypeUserBalances converter = FfiConverterTypeUserBalances.INSTANCE;
		using MemoryStream buffer = new();
		converter.Write(balances, new BigEndianStream(buffer));
		Assert.Equal(converter.AllocationSize(balances), (int)buffer.Position);

		buffer.Position = 0;
		UserBalances read = converter.Read(new BigEndianStream(buffer));
		Assert.Equal(balances, read);

		AccountBalances account = new(Security.ZEC, balances);
		Assert.Equal(0.5m, account.Viewed.Amount);
		Assert.Equal(
			account.Spendable + account.MinimumFees + account.ImmatureChange + account.ImmatureIncome + account.Viewed,
			account.MainBalance);
	}
}
