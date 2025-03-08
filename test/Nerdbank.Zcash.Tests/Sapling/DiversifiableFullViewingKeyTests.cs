// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Numerics;

namespace Sapling;

public class DiversifiableFullViewingKeyTests : TestBase
{
	private readonly ITestOutputHelper logger;

	public DiversifiableFullViewingKeyTests(ITestOutputHelper logger)
	{
		this.logger = logger;
	}

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
	public void TextEncoding_TryDecode(bool testNet)
	{
		ZcashNetwork network = testNet ? ZcashNetwork.TestNet : ZcashNetwork.MainNet;
		string expected = testNet
			? "zxviewtestsapling1qwgr6ehwqqqqpq8wxv4wuwhqzlfgam673vzk8eyq0s85t7ny3yszkrnqmmjg8yhk3tg909ph892t9exkp37sj66xd5u3juxfnd2xk6npdugecuesmn8svv5gd7u4gawlah8tdhzep0yvmk3hfy250ck7ezqyyzxff0xe7lpjaw2t56wcan7m3el97m22jj3u7s3qxdnw5qkm3tgm5878yn3zth7wrh99ecslcxm4n4vqm2jjusns4gu2c6kh3qwly625lvmyyaw5lagag7dry"
			: "zxviews1qveachkgqqqqpq99ju5nxh8zx5k5kkaeante0l3zcv0737su6jyadm4kk337qeu7h67grk26k7lyqvngnx53qt2gjqwatxctq3swj86n54sa34cg7f0gcff9ue6t7wqweh2q0he8lm9x2ela3kypl00f2wk6eypuzhl5pv3rkuwwm9dzxngcurfzgnqtpx4mvj7z8dpewx7edey0yaaatjnhjdnan4vqxvmny003n4l2ye9ey5nt5y3sqfy3r6l0ungptk2u2qgaxscq6f9ek";
		Zip32HDWallet wallet = new(Mnemonic, network);
		Zip32HDWallet.Sapling.ExtendedSpendingKey account = wallet.CreateSaplingAccount(0);
		string actual = account.ExtendedFullViewingKey.TextEncoding;
		this.logger.WriteLine(actual);
		Assert.Equal(expected, actual);

		Assert.True(Zip32HDWallet.Sapling.ExtendedFullViewingKey.TryDecode(actual, out _, out _, out Zip32HDWallet.Sapling.ExtendedFullViewingKey? decoded));
		Assert.Equal(account.ExtendedFullViewingKey, decoded);
	}
}
