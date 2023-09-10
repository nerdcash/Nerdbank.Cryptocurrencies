// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Numerics;

public class Zip32HDWalletTests : TestBase
{
	private readonly ITestOutputHelper logger;

	public Zip32HDWalletTests(ITestOutputHelper logger)
	{
		this.logger = logger;
	}

	[Theory, PairwiseData]
	public void CreateSaplingMasterKey(ZcashNetwork network)
	{
		Bip39Mnemonic mnemonic = Bip39Mnemonic.Create(32);
		this.logger.WriteLine($"Mnemonic: {mnemonic}");
		Zip32HDWallet.Sapling.ExtendedSpendingKey spendingKey = Zip32HDWallet.Sapling.Create(mnemonic, network);
		Assert.Equal(0, spendingKey.Depth);
		Assert.Equal(0u, spendingKey.ChildIndex);
		Assert.Equal(network, spendingKey.Network);
		Assert.NotNull(spendingKey.ExtendedFullViewingKey);
		Assert.NotEqual(default, spendingKey.ExtendedFullViewingKey.Fingerprint);
	}

	[Theory, PairwiseData]
	public void CreateOrchardMasterKey(ZcashNetwork network)
	{
		this.logger.WriteLine($"Mnemonic: {Mnemonic}");
		Zip32HDWallet zip32 = new(Mnemonic, network);
		Zip32HDWallet.Orchard.ExtendedSpendingKey masterSpendingKey = Zip32HDWallet.Orchard.Create(Mnemonic, network);
		Assert.Equal(0, masterSpendingKey.Depth);
		Assert.Equal(0u, masterSpendingKey.ChildIndex);
		Assert.Equal(network, masterSpendingKey.Network);
		Assert.NotNull(masterSpendingKey.FullViewingKey);

		Zip32HDWallet.Orchard.ExtendedSpendingKey accountSpendingKey = masterSpendingKey.Derive(zip32.CreateKeyPath(0));
		Assert.NotNull(accountSpendingKey.FullViewingKey);

		Assert.NotEqual(default, masterSpendingKey.Fingerprint);
	}

	[Fact]
	public void CreateOrchardAddressFromSeed()
	{
		Zip32HDWallet zip32 = new(Mnemonic, ZcashNetwork.MainNet);
		Zip32HDWallet.Orchard.ExtendedSpendingKey accountSpendingKey = zip32.CreateOrchardAccount(0);
		OrchardReceiver receiver = accountSpendingKey.IncomingViewingKey.CreateReceiver(0);
		OrchardAddress address = new(receiver);
		this.logger.WriteLine(address);
		Assert.Equal("u1zpfqm4r0cc5ttvt4mft6nvyqe3uwsdcgx65s44sd3ar42rnkz7v9az0ez7dpyxvjcyj9x0sd89yy7635vn8fplwvg6vn4tr6wqpyxqaw", address.Address);
	}

	[Fact]
	public void CreateSaplingAddressFromSeed()
	{
		Zip32HDWallet zip32 = new(Mnemonic, ZcashNetwork.MainNet);
		Zip32HDWallet.Sapling.ExtendedSpendingKey accountSpendingKey = zip32.CreateSaplingAccount(0);
		DiversifierIndex diversifierIndex = default;
		Assert.True(accountSpendingKey.IncomingViewingKey.TryCreateReceiver(ref diversifierIndex, out SaplingReceiver? receiver));
		Assert.Equal(1, diversifierIndex.ToBigInteger()); // index 0 was invalid in this case.
		SaplingAddress address = new(receiver.Value);
		this.logger.WriteLine(address);
		Assert.Equal("zs1duqpcc2ql7zfjttdm2gpawe8t5ecek5k834u9vdg4mqhw7j8j39sgjy8xguvk2semyd4ujeyj28", address.Address);
	}

	[Fact]
	public void CreateTransparentAddressFromSeed()
	{
		Zip32HDWallet zip32 = new(Mnemonic, ZcashNetwork.MainNet);
		Zip32HDWallet.Transparent.ExtendedSpendingKey accountSpendingKey = zip32.CreateTransparentAccount(0);
		Assert.Equal("t1VUs4Heab5xF36dTMX3n5DCjHew4SdUMGR", accountSpendingKey.FullViewingKey.DefaultAddress);
	}

	[Fact]
	public void CreateSaplingAddressFromSeed_ViaFVK()
	{
		Zip32HDWallet.Sapling.ExtendedFullViewingKey masterFullViewingKey = Zip32HDWallet.Sapling.Create(Mnemonic, ZcashNetwork.MainNet).ExtendedFullViewingKey;
		Zip32HDWallet.Sapling.ExtendedFullViewingKey childFVK = masterFullViewingKey.Derive(3);
		DiversifierIndex diversifierIndex = default;
		Assert.True(childFVK.IncomingViewingKey.TryCreateReceiver(ref diversifierIndex, out SaplingReceiver? receiver));
		Assert.Equal(3, diversifierIndex.ToBigInteger()); // indexes 0-2 were invalid in this case.
		SaplingAddress address = new(receiver.Value);
		this.logger.WriteLine(address);
		Assert.Equal("zs134p2zqc6lnrywwdrrm522f5745ctlvc0lnuvlpauwrrjydjrkkq7f4v98wkg669uf5zm54zlc8g", address.Address);
	}

	[Fact]
	public void MnemonicCtorInitializesProperties()
	{
		Zip32HDWallet wallet = new(Mnemonic, ZcashNetwork.TestNet);
		Assert.Equal(ZcashNetwork.TestNet, wallet.Network);
		Assert.Same(Mnemonic, wallet.Mnemonic);
		Assert.Equal(Mnemonic.Seed.ToArray(), wallet.Seed.ToArray());
	}

	[Fact]
	public void SeedCtorInitializesProperties()
	{
		Zip32HDWallet wallet = new(Mnemonic.Seed, ZcashNetwork.TestNet);
		Assert.Equal(ZcashNetwork.TestNet, wallet.Network);
		Assert.Null(wallet.Mnemonic);
		Assert.Equal(Mnemonic.Seed.ToArray(), wallet.Seed.ToArray());
	}

	[Fact]
	public void CreateOrchardAccount()
	{
		Zip32HDWallet wallet = new(Mnemonic, ZcashNetwork.TestNet);
		Zip32HDWallet.Orchard.ExtendedSpendingKey account = wallet.CreateOrchardAccount(1);
		Assert.Equal(1u | Bip32HDWallet.HardenedBit, account.ChildIndex);

		Assert.Equal(new OrchardAddress(account.IncomingViewingKey.CreateReceiver(0), ZcashNetwork.TestNet), account.DefaultAddress);
	}

	[Fact]
	public void CreateSaplingAccount()
	{
		Zip32HDWallet wallet = new(Mnemonic, ZcashNetwork.TestNet);
		Zip32HDWallet.Sapling.ExtendedSpendingKey account = wallet.CreateSaplingAccount(1);
		Assert.Equal(1u | Bip32HDWallet.HardenedBit, account.ChildIndex);

		DiversifierIndex diversifier = default;
		Assert.True(account.IncomingViewingKey.TryCreateReceiver(ref diversifier, out SaplingReceiver? receiver));
		Assert.Equal(new SaplingAddress(receiver.Value, ZcashNetwork.TestNet), account.DefaultAddress);
	}

	[Theory, PairwiseData]
	public void ExtendedSpendingKey_Sapling_Encoded_FromEncoded(bool testNet)
	{
		ZcashNetwork network = testNet ? ZcashNetwork.TestNet : ZcashNetwork.MainNet;
		string expected = testNet
			? "secret-extended-key-test1q04ysg8gqqqqpqzjkwjfwep2z7zdmwyrhgdc4gsq5s6u3qpd66mhecmy87uy3mv4et3gd9yj8cp2avd2u4u2wcydyy2ece7r23u5u4h5ns99meu89dvq29rx3hzpyjwjwtcj3c3jmfkutjlmhq3rflp7kremd9jy9d6pmpqvyc288459krkjym7kf66ndt56gl66p96dz2dk2scaysdfnmhgj6vjants29437g9qzsu408r0rpqnlqvn36plutfvcu02j3zk84gxfcc5yhxlf"
			: "secret-extended-key-main1qvqlw6pjqqqqpqrcxxrqd2acqcurzx7wat9gk7jv3wee36rj970d9h47fpg6fddwupcaun2z7rkqh4hdkkw2y6a2w4x32vg908tpyr63z4akzq4sz5fsm92zmal0puqq9ye7afkkakvg7aurtlrex03dahp7zgfay3dwdkc85t6662jk4lkv8kjughx6x8vrn97pqcqn04wduse3n5hhlkf6qc5ah08udx9g0wpkf6rausju7yamzl6z4gdyrtmqs9ak93w0z222vgsffenlf";
		Zip32HDWallet wallet = new(Mnemonic, network);
		Zip32HDWallet.Sapling.ExtendedSpendingKey account = wallet.CreateSaplingAccount(0);
		string actual = account.Encoded;
		this.logger.WriteLine(actual);
		Assert.Equal(expected, actual);

		var decoded = Zip32HDWallet.Sapling.ExtendedSpendingKey.FromEncoded(actual);
		Assert.Equal(account, decoded);
	}

	[Theory, PairwiseData]
	public void ExtendedSpendingKey_Orchard_Encoded_FromEncoded(bool testNet)
	{
		ZcashNetwork network = testNet ? ZcashNetwork.TestNet : ZcashNetwork.MainNet;
		string expected = testNet
			? "secret-orchard-extsk-test1q0dw68khqqqqpqqk2a8z5futn7y0hn0lkgezurg87xphz7gpmgygx3fzj5m48up3l36luspwl28gpqq8x78dyjyuusqnznsr8g0g0m4nvnh3a6m30m25266kfsj"
			: "secret-orchard-extsk-main1qwx8hu3mqqqqpqzr5shlv8gv794seyh4247z7htmgfq7weu7fsk55mxy79uvecdqcd08m73ahd2n4yquhr7uudf9wxhl39qeyjdlam82ue3jusfyxye8y3x62h0";
		Zip32HDWallet wallet = new(Mnemonic, network);
		Zip32HDWallet.Orchard.ExtendedSpendingKey account = wallet.CreateOrchardAccount(0);
		string actual = account.Encoded;
		this.logger.WriteLine(actual);
		Assert.Equal(expected, actual);

		var decoded = Zip32HDWallet.Orchard.ExtendedSpendingKey.FromEncoded(actual);
		Assert.Equal(account, decoded);
	}
}
