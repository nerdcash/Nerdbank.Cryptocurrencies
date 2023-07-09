// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Numerics;

namespace Sapling;

public class DiversifiableFullViewingKeyTests : TestBase
{
	[Fact]
	public void TryGetDiversifierIndex_And_CheckReceiver()
	{
		Zip32HDWallet wallet = new(Mnemonic, ZcashNetwork.TestNet);
		Zip32HDWallet.Sapling.ExtendedSpendingKey account1 = wallet.CreateSaplingAccount(0);
		Zip32HDWallet.Sapling.ExtendedSpendingKey account2 = wallet.CreateSaplingAccount(1);
		BigInteger expectedIndex = 3;
		Assert.True(account1.FullViewingKey.Key.TryCreateReceiver(ref expectedIndex, out SaplingReceiver receiver));

		Assert.True(account1.FullViewingKey.Key.CheckReceiver(receiver));
		Assert.True(account1.FullViewingKey.Key.TryGetDiversifierIndex(receiver, out BigInteger? idx));
		Assert.Equal(expectedIndex, idx);

		Assert.False(account2.FullViewingKey.Key.CheckReceiver(receiver));
		Assert.False(account2.FullViewingKey.Key.TryGetDiversifierIndex(receiver, out idx));
		Assert.Null(idx);
	}
}
