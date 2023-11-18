// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using DynamicData;
using Nerdbank.Cryptocurrencies.Exchanges;

namespace ViewModels;

public class HistoryViewModelTests : ViewModelTestBase
{
	private HistoryViewModel viewModel;

	public HistoryViewModelTests()
	{
		this.viewModel = new(this.MainViewModel);
		this.viewModel.Transactions.AddRange(new TransactionViewModel[]
		{
			// These transactions are deliberately NOT inserted in date-order, because we test
			// the sorting function in one of the tests.
			new(new ZcashTransaction { Amount = this.ZEC(-0.5m), IsIncoming = false,  Memo = "Hot Chocolate", TransactionId = "1e62b7", When = DateTimeOffset.Now - TimeSpan.FromDays(35) }, this.MainViewModel) { OtherPartyName = "Red Rock Cafe" },
			new(new ZcashTransaction { Memo = "For the pizza", TransactionId = "12345abc", When = DateTimeOffset.Now - TimeSpan.FromDays(200), Amount = this.ZEC(1.2345m), IsIncoming = true }, this.MainViewModel) { OtherPartyName = "Andrew Arnott" },
			new(new ZcashTransaction { Amount = this.ZEC(2m), IsIncoming = true, Memo = "Paycheck", TransactionId = "236ba", When = DateTimeOffset.Now - TimeSpan.FromDays(2) }, this.MainViewModel) { OtherPartyName = "Employer" },
		});
	}

	[Fact]
	public void TransactionsSortedByDate()
	{
		IOrderedEnumerable<TransactionViewModel> sortedTransactions =
			this.viewModel.Transactions.OrderBy(t => t.When);

		// Assert that the transactions are sorted by date
		Assert.Equal(sortedTransactions, this.viewModel.Transactions);
	}

	[Fact]
	public void Balance()
	{
		SecurityAmount runningBalance = this.viewModel.SelectedSecurity.Amount(0);
		foreach (TransactionViewModel tx in this.viewModel.Transactions)
		{
			runningBalance += tx.Amount;
			Assert.Equal(runningBalance, tx.RunningBalance);
		}
	}

	private SecurityAmount ZEC(decimal amount) => this.viewModel.SelectedSecurity.Amount(amount);
}
