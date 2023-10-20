// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Zcash.Sapling;

namespace Sapling;

public class FullViewingKeyTests : TestBase
{
	private readonly ITestOutputHelper logger;

	public FullViewingKeyTests(ITestOutputHelper logger)
	{
		this.logger = logger;
	}

	[Theory, PairwiseData]
	public void TextEncoding_TryDecode(bool testNet)
	{
		ZcashNetwork network = testNet ? ZcashNetwork.TestNet : ZcashNetwork.MainNet;
		string expected = testNet
			? "zviewtestsapling15cr64vjtd0x7xh6ytmun4ulp7k93th7xunhrkqrf55q82m892fr6n708hdlq20gj2ydxz2n5ps052lmz20w2ykxfr9dzwu8fnmktv6fxz3eadpdsa53xl4jwk5m2axj87ksfwngjndj5x8fyr2v7a6ykny7t049p"
			: "zviews1lxdtxcc28jx4anvr49m8qz6rdvv6zuff49vc7vj3gmxzkq0vhlkmcvdmv6a0sm2x9rfdf26xcr34xuhyk9sxfct86ylqwwrf6w6z739z7kkj5440anpa5hz9ek33mque0sgxqymatn0yxvva9alajwsx9yd8x6ty";
		Zip32HDWallet wallet = new(Mnemonic, network);
		Zip32HDWallet.Sapling.ExtendedSpendingKey account = wallet.CreateSaplingAccount(0);
		string actual = account.FullViewingKey.TextEncoding;
		this.logger.WriteLine(actual);
		Assert.Equal(expected, actual);

		Assert.True(FullViewingKey.TryDecode(actual, out _, out _, out FullViewingKey? decoded));
		Assert.Equal(account.FullViewingKey, decoded);
	}
}
