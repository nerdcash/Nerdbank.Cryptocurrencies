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
			MockTx(-0.5m, "Hot Chocolate", TimeSpan.FromDays(35), "e5e259b8ef7f0cca708031ab0f10e2a3aa48e069a0817d3a54f71c7f56e0110d", "Red Rock Cafe"),
			MockTx(1.2345m, "For the pizza", TimeSpan.FromDays(200), "9c1952fbaf5389fa8c36c45f17b2e303c33a9074dee8d90c694ee14112e0f46d", "Andrew Arnott"),
			MockTx(2m, "Paycheck", TimeSpan.FromDays(2), "4e5f72b5eb58018daf506a13a5ccd9cb6b7657fd9f9ac4a8c297a51b5499ed9b", "Employer"),
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
					TransactionId = TxId.Parse(txid),
					When = DateTimeOffset.UtcNow - age,
					SendItems = sends,
					RecvItems = receives,
					Fee = 0.0001m,
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
		ImmutableArray<TransactionViewModel> sortedTransactions =
			this.viewModel.Transactions.ToImmutableArray().Sort(TransactionChronologicalComparer.NewestToOldest);

		// Assert that the transactions are sorted by date
		Assert.Equal(sortedTransactions, this.viewModel.Transactions);
	}

	[Fact]
	public void Balance()
	{
		SecurityAmount runningBalance = this.viewModel.SelectedSecurity.Amount(0);
		foreach (TransactionViewModel tx in this.viewModel.Transactions)
		{
			runningBalance += tx.NetChange;
			Assert.Equal(runningBalance, tx.RunningBalance);
		}
	}

	private SecurityAmount ZEC(decimal amount) => this.viewModel.SelectedSecurity.Amount(amount);
}
