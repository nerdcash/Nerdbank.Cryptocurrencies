// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public class Zip32HDWalletTests : TestBase
{
	private readonly ITestOutputHelper logger;

	public Zip32HDWalletTests(ITestOutputHelper logger)
	{
		this.logger = logger;
	}

	[Theory, PairwiseData]
	public void CreateSaplingMasterKey(bool testNet)
	{
		Bip39Mnemonic mnemonic = Bip39Mnemonic.Create(32);
		this.logger.WriteLine($"Mnemonic: {mnemonic}");
		Zip32HDWallet.Sapling.ExtendedSpendingKey spendingKey = Zip32HDWallet.Sapling.Create(mnemonic, testNet);
		Assert.Equal(0, spendingKey.Depth);
		Assert.Equal(0u, spendingKey.ChildNumber);
		Assert.Equal(testNet, spendingKey.IsTestNet);
		Assert.NotNull(spendingKey.SpendingKey);
		Assert.NotNull(spendingKey.FullViewingKey);
		Assert.NotEqual(0, spendingKey.FullViewingKey.Fingerprint.Length);
	}

	[Theory, PairwiseData]
	public void CreateOrchardMasterKey(bool testNet)
	{
		Bip39Mnemonic mnemonic = Bip39Mnemonic.Parse("badge bless baby bird anger wage memory extend word isolate equip faith");
		this.logger.WriteLine($"Mnemonic: {mnemonic}");
		Zip32HDWallet.Orchard.ExtendedSpendingKey masterSpendingKey = Zip32HDWallet.Orchard.Create(mnemonic, testNet);
		Assert.Equal(0, masterSpendingKey.Depth);
		Assert.Equal(0u, masterSpendingKey.ChildNumber);
		Assert.Equal(testNet, masterSpendingKey.IsTestNet);
		Assert.NotNull(masterSpendingKey.FullViewingKey);

		Zip32HDWallet.Orchard.ExtendedSpendingKey accountSpendingKey = masterSpendingKey.Derive(Zip32HDWallet.CreateKeyPath(0));
		Assert.NotNull(accountSpendingKey.FullViewingKey);

		Assert.NotEqual(default, masterSpendingKey.FullViewingKey.Fingerprint);
	}

	[Fact]
	public void CreateOrchardAddressFromSeed()
	{
		Bip39Mnemonic mnemonic = Bip39Mnemonic.Parse("badge bless baby bird anger wage memory extend word isolate equip faith");
		Zip32HDWallet.Orchard.ExtendedSpendingKey masterSpendingKey = Zip32HDWallet.Orchard.Create(mnemonic);
		Zip32HDWallet.Orchard.ExtendedSpendingKey accountSpendingKey = masterSpendingKey.Derive(Zip32HDWallet.CreateKeyPath(0));
		OrchardReceiver receiver = accountSpendingKey.FullViewingKey.Key.CreateReceiver(0);
		OrchardAddress address = new(receiver);
		this.logger.WriteLine(address);
		Assert.Equal("u1zpfqm4r0cc5ttvt4mft6nvyqe3uwsdcgx65s44sd3ar42rnkz7v9az0ez7dpyxvjcyj9x0sd89yy7635vn8fplwvg6vn4tr6wqpyxqaw", address.Address);
	}

	[Fact]
	public void CreateSaplingAddressFromSeed()
	{
		Bip39Mnemonic mnemonic = Bip39Mnemonic.Parse("badge bless baby bird anger wage memory extend word isolate equip faith");
		Zip32HDWallet.Sapling.ExtendedSpendingKey masterSpendingKey = Zip32HDWallet.Sapling.Create(mnemonic);
		Zip32HDWallet.Sapling.ExtendedSpendingKey accountSpendingKey = masterSpendingKey.Derive(Zip32HDWallet.CreateKeyPath(0));
		Assert.True(accountSpendingKey.FullViewingKey.Key.TryCreateReceiver(0, out SaplingReceiver receiver));
		SaplingAddress address = new(receiver);
		this.logger.WriteLine(address);
		Assert.Equal("zs1duqpcc2ql7zfjttdm2gpawe8t5ecek5k834u9vdg4mqhw7j8j39sgjy8xguvk2semyd4ujeyj28", address.Address);
	}
}
