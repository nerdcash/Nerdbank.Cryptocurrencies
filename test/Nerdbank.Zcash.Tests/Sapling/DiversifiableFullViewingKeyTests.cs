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
		DiversifierIndex expectedIndex = 3;
		Assert.True(account1.IncomingViewingKey.TryCreateReceiver(ref expectedIndex, out SaplingReceiver? receiver));

		Assert.True(account1.FullViewingKey.CheckReceiver(receiver.Value));
		Assert.True(account1.FullViewingKey.TryGetDiversifierIndex(receiver.Value, out DiversifierIndex? idx));
		Assert.Equal(expectedIndex, idx);

		Assert.False(account2.FullViewingKey.CheckReceiver(receiver.Value));
		Assert.False(account2.FullViewingKey.TryGetDiversifierIndex(receiver.Value, out idx));
		Assert.Null(idx);
	}

	[Theory, PairwiseData]
	public void Encoded_FromEncoded(bool testNet)
	{
		ZcashNetwork network = testNet ? ZcashNetwork.TestNet : ZcashNetwork.MainNet;
		string expected = testNet
			? "zxviewtestsapling1qvqlw6pjqqqqpqrcxxrqd2acqcurzx7wat9gk7jv3wee36rj970d9h47fpg6fddwurue4vmrpg7g6hkdsw5hvuqtgd43ngt39x54nrej29rvc2cpajl7m0p3hdnt47rdgc5d949tgmqwx5mjujckqe8pvlgnupecd8fmgt6y5t6662jk4lkv8kjughx6x8vrn97pqcqn04wduse3n5hhlkf6qc5ah08udx9g0wpkf6rausju7yamzl6z4gdyrtmqs9ak93w0z222vgs2h9a8j"
			: "zxviews1qvqlw6pjqqqqpqrcxxrqd2acqcurzx7wat9gk7jv3wee36rj970d9h47fpg6fddwurue4vmrpg7g6hkdsw5hvuqtgd43ngt39x54nrej29rvc2cpajl7m0p3hdnt47rdgc5d949tgmqwx5mjujckqe8pvlgnupecd8fmgt6y5t6662jk4lkv8kjughx6x8vrn97pqcqn04wduse3n5hhlkf6qc5ah08udx9g0wpkf6rausju7yamzl6z4gdyrtmqs9ak93w0z222vgst458pe";
		Zip32HDWallet wallet = new(Mnemonic, network);
		Zip32HDWallet.Sapling.ExtendedSpendingKey account = wallet.CreateSaplingAccount(0);
		string actual = account.ExtendedFullViewingKey.Encoded;
		Assert.Equal(expected, actual);

		var decoded = Zip32HDWallet.Sapling.ExtendedFullViewingKey.FromEncoded(actual);
		Assert.Equal(account.ExtendedFullViewingKey, decoded);
	}
}
