// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Numerics;
using Nerdbank.Zcash.Sapling;

namespace Sapling;

public class IncomingViewingKeyTests : TestBase
{
	private readonly ITestOutputHelper logger;

	public IncomingViewingKeyTests(ITestOutputHelper logger)
	{
		this.logger = logger;
	}

	[Theory, PairwiseData]
	public void TextEncoding_TryDecode(bool testNet)
	{
		ZcashNetwork network = testNet ? ZcashNetwork.TestNet : ZcashNetwork.MainNet;
		string expected = testNet
			? "zivktestsapling1yugfl5hmjt5wk4xx5atkculk54c630mu099sdthnemvzmfjemczqjt4zz2"
			: "zivks184h858g2g87ucf4jp3vqr0legsts34cn60xptenyz72rdrwvlvzsfwkpqh";
		Zip32HDWallet wallet = new(Mnemonic, network);
		Zip32HDWallet.Sapling.ExtendedSpendingKey account = wallet.CreateSaplingAccount(0);
		string actual = account.FullViewingKey.IncomingViewingKey.TextEncoding;
		this.logger.WriteLine(actual);
		Assert.Equal(expected, actual);

		Assert.True(IncomingViewingKey.TryDecode(actual, out DecodeError? decodeError, out string? errorMessage, out IncomingViewingKey? key));
		Assert.Null(decodeError);
		Assert.Null(errorMessage);
		Assert.Equal(account.FullViewingKey.IncomingViewingKey, key);
	}

	[Fact]
	public void TryGetDiversifierIndex_And_CheckReceiver()
	{
		Zip32HDWallet wallet = new(Mnemonic, ZcashNetwork.TestNet);
		Zip32HDWallet.Sapling.ExtendedSpendingKey account1 = wallet.CreateSaplingAccount(0);
		Zip32HDWallet.Sapling.ExtendedSpendingKey account2 = wallet.CreateSaplingAccount(1);
		DiversifierIndex index = 3;
		Assert.True(account1.IncomingViewingKey.TryCreateReceiver(ref index, out SaplingReceiver? receiver));

		Assert.True(account1.IncomingViewingKey.CheckReceiver(receiver.Value));
		Assert.True(account1.IncomingViewingKey.TryGetDiversifierIndex(receiver.Value, out DiversifierIndex? idx));
		Assert.Equal(index, idx);

		Assert.False(account2.IncomingViewingKey.CheckReceiver(receiver.Value));
		Assert.False(account2.IncomingViewingKey.TryGetDiversifierIndex(receiver.Value, out idx));
		Assert.Null(idx);
	}
}
