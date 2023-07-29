// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Zcash.Sapling;

namespace Sapling;

public class FullViewingKeyTests : TestBase
{
	[Theory, PairwiseData]
	public void Encoded_FromEncoded(bool testNet)
	{
		ZcashNetwork network = testNet ? ZcashNetwork.TestNet : ZcashNetwork.MainNet;
		string expected = testNet
			? "zviewtestsapling1lxdtxcc28jx4anvr49m8qz6rdvv6zuff49vc7vj3gmxzkq0vhlkmcvdmv6a0sm2x9rfdf26xcr34xuhyk9sxfct86ylqwwrf6w6z739z7kkj5440anpa5hz9ek33mque0sgxqymatn0yxvva9alajwsx9ykagj2z"
			: "zviews1lxdtxcc28jx4anvr49m8qz6rdvv6zuff49vc7vj3gmxzkq0vhlkmcvdmv6a0sm2x9rfdf26xcr34xuhyk9sxfct86ylqwwrf6w6z739z7kkj5440anpa5hz9ek33mque0sgxqymatn0yxvva9alajwsx9yd8x6ty";
		Zip32HDWallet wallet = new(Mnemonic, network);
		Zip32HDWallet.Sapling.ExtendedSpendingKey account = wallet.CreateSaplingAccount(0);
		string actual = account.FullViewingKey.Key.Encoded;
		Assert.Equal(expected, actual);

		var decoded = FullViewingKey.FromEncoded(actual);
		Assert.Equal(account.FullViewingKey.Key, decoded);
	}
}
