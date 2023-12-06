// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
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
			MockTx(-0.5m, "Hot Chocolate", TimeSpan.FromDays(35), "1e62b7", "Red Rock Cafe"),
			MockTx(1.2345m, "For the pizza", TimeSpan.FromDays(200), "12345abc", "Andrew Arnott"),
			MockTx(2m, "Paycheck", TimeSpan.FromDays(2), "236ba", "Employer"),
		});

		TransactionViewModel MockTx(decimal amount, string memo, TimeSpan age, string txid, string otherPartyName)
		{
			ImmutableArray<ZcashTransaction.LineItem> sends = amount < 0
				? [new ZcashTransaction.LineItem { Amount = -amount, Memo = Memo.FromMessage(memo), ToAddress = ZcashAddress.Decode("t1N7bGKWqoWVrv4XGSzrfUsoKkCxNFAutQZ") }]
				: [];
			ImmutableArray<ZcashTransaction.LineItem> receives = amount > 0
				? [new ZcashTransaction.LineItem { Amount = amount, Memo = Memo.FromMessage(memo), ToAddress = ZcashAddress.Decode("t1XkdzRXnCguCQtrigDUtgyz5SVtLYaAXBi") }]
				: [];
			return new TransactionViewModel(
				new TradingPair(Security.USD, this.viewModel.SelectedSecurity),
				new ZcashTransaction
				{
					IsIncoming = amount > 0,
					TransactionId = txid,
					When = DateTimeOffset.UtcNow - age,
					SendItems = sends,
					RecvItems = receives,
					Fee = -0.0001m,
				},
				this.viewModel)
			{
				OtherPartyName = otherPartyName,
			};
		}
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
