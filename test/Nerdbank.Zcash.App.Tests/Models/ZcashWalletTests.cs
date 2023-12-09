// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Models;

public class ZcashWalletTests : ModelTestBase<ZcashWallet>
{
	public ZcashWalletTests(ITestOutputHelper logger)
		: base(logger)
	{
	}

	public override ZcashWallet Model => this.Wallet;

	public ZcashWallet Wallet { get; } = new();

	[Fact]
	public void SerializeWithHDAndLoneAccounts()
	{
		Zip32HDWallet zip32 = new(Mnemonic, ZcashNetwork.TestNet);
		Account hd1a = this.Wallet.Add(new ZcashAccount(zip32) { BirthdayHeight = 123456 });
		Account hd1b = this.Wallet.Add(new ZcashAccount(zip32, 3) { BirthdayHeight = 123456 });

		Account lone1 = this.Wallet.Add(new ZcashAccount(new ZcashAccount(zip32, 5).IncomingViewing.UnifiedKey) { BirthdayHeight = 123456 });

		ZcashWallet deserialized = this.SerializeRoundtrip();

		HDWallet deserializedHD = Assert.Single(deserialized.HDWallets);
		Account deserializedLone = Assert.Single(deserialized.Accounts, a => a.ZcashAccount.HDDerivation is null);
		Assert.Equal(2, deserialized.Accounts.Count(a => a.ZcashAccount.HDDerivation is not null));
		Account deserializedHD1a = deserialized.Accounts.Single(a => a.ZcashAccount.HDDerivation is { AccountIndex: 0 });
		Account deserializedHD1b = deserialized.Accounts.Single(a => a.ZcashAccount.HDDerivation is { AccountIndex: 3 });
		Assert.Equal<uint?>(0, deserializedHD1a.ZcashAccount.HDDerivation?.AccountIndex);
		Assert.Equal<uint?>(3, deserializedHD1b.ZcashAccount.HDDerivation?.AccountIndex);
	}

	[Fact]
	public void GetMaxAccountIndex()
	{
		HDWallet hd = new(Mnemonic) { Name = "HD Test" };
		Assert.Null(this.Model.GetMaxAccountIndex(hd, ZcashNetwork.MainNet));

		this.Model.Add(new ZcashAccount(hd.MainNet, 3) { BirthdayHeight = 123456 });

		Assert.Equal(3u, this.Model.GetMaxAccountIndex(hd, ZcashNetwork.MainNet));
	}
}
