// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Numerics;
using Nerdbank.Zcash.Sapling;

namespace Sapling;

public class IncomingViewingKeyTests : TestBase
{
	[Theory, PairwiseData]
	public void Encoded_FromEncoded(bool testNet)
	{
		ZcashNetwork network = testNet ? ZcashNetwork.TestNet : ZcashNetwork.MainNet;
		string expected = testNet
			? "zivktestsapling184h858g2g87ucf4jp3vqr0legsts34cn60xptenyz72rdrwvlvzs6eatnc"
			: "zivks184h858g2g87ucf4jp3vqr0legsts34cn60xptenyz72rdrwvlvzsfwkpqh";
		Zip32HDWallet wallet = new(Mnemonic, network);
		Zip32HDWallet.Sapling.ExtendedSpendingKey account = wallet.CreateSaplingAccount(0);
		string actual = account.FullViewingKey.IncomingViewingKey.Encoded;
		Assert.Equal(expected, actual);

		var decoded = IncomingViewingKey.FromEncoded(actual);
		Assert.Equal(account.FullViewingKey.IncomingViewingKey, decoded);
	}

	[Fact]
	public void TryGetDiversifierIndex_And_CheckReceiver()
	{
		Zip32HDWallet wallet = new(Mnemonic, ZcashNetwork.TestNet);
		Zip32HDWallet.Sapling.ExtendedSpendingKey account1 = wallet.CreateSaplingAccount(0);
		Zip32HDWallet.Sapling.ExtendedSpendingKey account2 = wallet.CreateSaplingAccount(1);
		BigInteger index = 3;
		Assert.True(account1.IncomingViewingKey.TryCreateReceiver(ref index, out SaplingReceiver receiver));

		Assert.True(account1.IncomingViewingKey.CheckReceiver(receiver));
		Assert.True(account1.IncomingViewingKey.TryGetDiversifierIndex(receiver, out BigInteger? idx));
		Assert.Equal(index, idx);

		Assert.False(account2.IncomingViewingKey.CheckReceiver(receiver));
		Assert.False(account2.IncomingViewingKey.TryGetDiversifierIndex(receiver, out idx));
		Assert.Null(idx);
	}
}
