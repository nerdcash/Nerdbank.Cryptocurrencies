// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Models;

public class ZcashTransactionTests : ModelTestBase<ZcashTransaction>
{
	public ZcashTransactionTests(ITestOutputHelper logger)
		: base(logger)
	{
	}

	public ZcashTransaction Transaction { get; set; } = new ZcashTransaction
	{
		IsIncoming = false,
		TransactionId = TxId.Parse("e5e259b8ef7f0cca708031ab0f10e2a3aa48e069a0817d3a54f71c7f56e0110d"),
		When = DateTimeOffset.UtcNow - TimeSpan.FromDays(2),
		SendItems = [new() { Amount = 2, ToAddress = ZcashAddress.Decode("t1N7bGKWqoWVrv4XGSzrfUsoKkCxNFAutQZ"), Memo = Memo.NoMemo }],
		RecvItems = [new() { Amount = 0.1m, ToAddress = ZcashAddress.Decode("zs1znewe2leucm8gsd2ue24kvp3jjjwgrhmytmv0scenaf460kdj70r299a88r8n0pyvwz7c9skfmy"), Memo = Memo.NoMemo }],
		Fee = 0.0001m,
	};

	public override ZcashTransaction Model => this.Transaction;

	[Fact]
	public void FeeChangeRaisesPropertyChangedForFee()
	{
		Assert.PropertyChanged(this.Model, nameof(this.Model.Fee), () => this.Model.Fee += 0.0001m);
	}

	[Fact]
	public void FeeChangeRaisesPropertyChangedForFeeNetChange()
	{
		Assert.PropertyChanged(this.Model, nameof(this.Model.NetChange), () => this.Model.Fee += 0.0001m);
	}
}
