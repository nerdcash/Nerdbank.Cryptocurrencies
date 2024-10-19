// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

namespace ViewModels;

public class HistoryViewModelTests : ViewModelTestBase
{
	private HistoryViewModel viewModel;

	public HistoryViewModelTests()
	{
		this.viewModel = new(this.MainViewModel);

		// These transactions are deliberately NOT inserted in date-order, because we test
		// the sorting function in one of the tests.
		this.viewModel.Transactions.AddRange(
		[
			this.MockTx(-0.5m, "Hot Chocolate", TimeSpan.FromDays(35), "e5e259b8ef7f0cca708031ab0f10e2a3aa48e069a0817d3a54f71c7f56e0110d", "Red Rock Cafe"),
			this.MockTx(1.2345m, "For the pizza", TimeSpan.FromDays(200), "9c1952fbaf5389fa8c36c45f17b2e303c33a9074dee8d90c694ee14112e0f46d", "Andrew Arnott"),
			this.MockTx(2m, "Paycheck", TimeSpan.FromDays(2), "4e5f72b5eb58018daf506a13a5ccd9cb6b7657fd9f9ac4a8c297a51b5499ed9b", "Employer"),
		]);
	}

	[UIFact]
	public void TransactionsSortedByDate()
	{
		ImmutableArray<TransactionViewModel> sortedTransactions =
			this.viewModel.Transactions.ToImmutableArray().Sort(TransactionChronologicalComparer.NewestToOldest);

		// Assert that the transactions are sorted by date
		Assert.Equal(sortedTransactions, this.viewModel.Transactions);
	}

	[UIFact]
	public void ReorderTransactionsWithWhenChange()
	{
		// Move the transaction that is in the middle up to the top position by changing its When column.
		TransactionViewModel targetTransaction = this.viewModel.Transactions[1];
		TransactionViewModel topTransaction = this.viewModel.Transactions[0];
		targetTransaction.Model.When = DateTimeOffset.UtcNow;
		Assert.Same(targetTransaction, this.viewModel.Transactions[0]);
		Assert.Same(topTransaction, this.viewModel.Transactions[1]);
	}

	[UIFact]
	public void FeeChangeUpdatesRunningBalance()
	{
		TransactionViewModel tx = this.viewModel.Transactions[1];
		SecurityAmount runningBalance = tx.RunningBalance;
		decimal feeChange = 0.0001m;
		tx.Model.Fee += feeChange;
		Assert.Equal(runningBalance.Amount - feeChange, tx.RunningBalance.Amount);
	}

	[UIFact]
	public void Balance_Initial()
	{
		this.AssertRunningBalances();
	}

	[UIFact]
	public void Balance_AfterInsertingOneAtTop()
	{
		this.viewModel.Transactions.Add(this.MockTx(3.2m, "Inserted", TimeSpan.Zero, "005f72b5eb58018daf506a13a5ccd9cb6b7657fd9f9ac4a8c297a51b5499ed9b", "somebody"));
		this.AssertRunningBalances();
	}

	[UIFact]
	public void Balance_AfterInsertingTwoAtTop()
	{
		this.viewModel.Transactions.AddRange([
			this.MockTx(3.2m, "Inserted 2nd", TimeSpan.FromDays(1), "005f72b5eb58018daf506a13a5ccd9cb6b7657fd9f9ac4a8c297a51b5499ed9b", "somebody"),
			this.MockTx(3.2m, "Inserted Top", TimeSpan.Zero, "015f72b5eb58018daf506a13a5ccd9cb6b7657fd9f9ac4a8c297a51b5499ed9b", "somebody"),
		]);
		this.AssertRunningBalances();
	}

	[UIFact]
	public void Balance_AfterInsertingOneInTheMiddle()
	{
		this.viewModel.Transactions.Add(this.MockTx(3.2m, "Inserted", TimeSpan.FromDays(5), "005f72b5eb58018daf506a13a5ccd9cb6b7657fd9f9ac4a8c297a51b5499ed9b", "somebody"));
		this.AssertRunningBalances();
	}

	[UIFact]
	public void Balance_AfterInsertingTwoInTheMiddle()
	{
		this.viewModel.Transactions.AddRange([
			this.MockTx(3.2m, "Inserted 2nd", TimeSpan.FromDays(6), "005f72b5eb58018daf506a13a5ccd9cb6b7657fd9f9ac4a8c297a51b5499ed9b", "somebody"),
			this.MockTx(3.2m, "Inserted 1st", TimeSpan.FromDays(5), "015f72b5eb58018daf506a13a5ccd9cb6b7657fd9f9ac4a8c297a51b5499ed9b", "somebody"),
		]);
		this.AssertRunningBalances();
	}

	[UIFact]
	public void Balance_AfterInsertingOneAtBottom()
	{
		this.viewModel.Transactions.Add(this.MockTx(3.2m, "Inserted", TimeSpan.FromDays(300), "005f72b5eb58018daf506a13a5ccd9cb6b7657fd9f9ac4a8c297a51b5499ed9b", "somebody"));
		this.AssertRunningBalances();
	}

	[UIFact]
	public void Balance_AfterInsertingTwoAtBottom()
	{
		this.viewModel.Transactions.AddRange([
			this.MockTx(3.2m, "Inserted 2nd", TimeSpan.FromDays(301), "005f72b5eb58018daf506a13a5ccd9cb6b7657fd9f9ac4a8c297a51b5499ed9b", "somebody"),
			this.MockTx(3.2m, "Inserted 1st", TimeSpan.FromDays(300), "015f72b5eb58018daf506a13a5ccd9cb6b7657fd9f9ac4a8c297a51b5499ed9b", "somebody"),
		]);
		this.AssertRunningBalances();
	}

	private void AssertRunningBalances()
	{
		SecurityAmount runningBalance = this.viewModel.SelectedSecurity.Amount(0);
		foreach (TransactionViewModel tx in this.viewModel.Transactions.OrderBy(t => t, TransactionChronologicalComparer.OldestToNewest))
		{
			runningBalance += tx.NetChange;
			Assert.Equal(runningBalance, tx.RunningBalance);
		}
	}

	private SecurityAmount ZEC(decimal amount) => this.viewModel.SelectedSecurity.Amount(amount);

	private TransactionViewModel MockTx(decimal amount, string memo, TimeSpan age, string txid, string otherPartyName)
	{
		ImmutableArray<ZcashTransaction.LineItem> sends = amount < 0
			? [new ZcashTransaction.LineItem { Amount = -amount, Memo = Memo.FromMessage(memo), ToAddress = ZcashAddress.Decode("t1N7bGKWqoWVrv4XGSzrfUsoKkCxNFAutQZ") }]
			: [];
		ImmutableArray<ZcashTransaction.LineItem> receives = amount > 0
			? [new ZcashTransaction.LineItem { Amount = amount, Memo = Memo.FromMessage(memo), ToAddress = ZcashAddress.Decode("t1XkdzRXnCguCQtrigDUtgyz5SVtLYaAXBi") }]
			: [];
		return new TransactionViewModel(
			this.viewModel.SelectedSecurity,
			Security.USD,
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
