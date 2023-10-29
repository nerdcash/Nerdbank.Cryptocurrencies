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
		var zip32 = new Zip32HDWallet(Mnemonic, ZcashNetwork.TestNet);
		Account hd1a = this.Wallet.Add(new ZcashAccount(zip32));
		Account hd1b = hd1a.MemberOf!.AddAccount(3);

		Account lone1 = this.Wallet.Add(new ZcashAccount(new ZcashAccount(zip32, 5).IncomingViewing.UnifiedKey));

		ZcashWallet deserialized = this.SerializeRoundtrip();

		HDWallet deserializedHD = Assert.Single(deserialized.HDWallets);
		Account deserializedLone = Assert.Single(deserialized.LoneAccounts);
		Assert.Equal(2, deserializedHD.Accounts.Count);
		Assert.True(deserializedHD.Accounts.TryGetValue(0, out Account? deserializedHD1a));
		Assert.True(deserializedHD.Accounts.TryGetValue(3, out Account? deserializedHD1b));
		Assert.Equal<uint?>(0, deserializedHD1a.ZcashAccount.HDDerivation?.AccountIndex);
		Assert.Equal<uint?>(3, deserializedHD1b.ZcashAccount.HDDerivation?.AccountIndex);
	}
}
