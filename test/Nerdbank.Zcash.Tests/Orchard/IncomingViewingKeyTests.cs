// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Numerics;

namespace Orchard;

public class IncomingViewingKeyTests : TestBase
{
	[Fact]
	public void TryGetDiversifierIndex_And_CheckReceiver()
	{
		Zip32HDWallet wallet = new(Mnemonic, ZcashNetwork.TestNet);
		Zip32HDWallet.Orchard.ExtendedSpendingKey account1 = wallet.CreateOrchardAccount(0);
		Zip32HDWallet.Orchard.ExtendedSpendingKey account2 = wallet.CreateOrchardAccount(1);
		OrchardReceiver receiver = account1.IncomingViewingKey.CreateReceiver(3);

		Assert.True(account1.IncomingViewingKey.CheckReceiver(receiver));
		Assert.True(account1.IncomingViewingKey.TryGetDiversifierIndex(receiver, out BigInteger? idx));
		Assert.Equal(3, idx);

		Assert.False(account2.IncomingViewingKey.CheckReceiver(receiver));
		Assert.False(account2.IncomingViewingKey.TryGetDiversifierIndex(receiver, out idx));
		Assert.Null(idx);
	}
}
