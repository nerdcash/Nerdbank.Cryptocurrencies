// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
}
