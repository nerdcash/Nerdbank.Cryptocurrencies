// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public class TransactionLineItemTests : TestBase
{
	[Test]
	public void Pool_Unspecified_StillInfersOrchard()
	{
		ZcashAccount account = new(new Zip32HDWallet(new byte[32], ZcashNetwork.TestNet), 0);
		OrchardAddress address = new(account.DefaultAddress.GetPoolReceiver<OrchardReceiver>()!.Value, account.Network);
		Transaction.LineItem item = new(address, 1m, Memo.NoMemo);

		Assert.Equal(Pool.Orchard, item.Pool);
		Assert.Equal(Pool.Ironwood, (item with { Pool = Pool.Ironwood }).Pool);
	}
}
