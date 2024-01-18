// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Bitcoin;

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
		Bip39Mnemonic mnemonic = Bip39Mnemonic.Create(Zip32HDWallet.MinimumEntropyLengthInBits);
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
		Assert.Equal("u1su5vtweds443eqwwzxtgmx4m2kxhwgax4hzm6xhxc6kugsakda3t3t0ae5nemwhlfwqw7uh2mvdgyg4pruu2t0dse02f2adpjv8pw35s", address.Address);
	}

	[Fact]
	public void CreateSaplingAddressFromSeed()
	{
		Zip32HDWallet zip32 = new(Mnemonic, ZcashNetwork.MainNet);
		Zip32HDWallet.Sapling.ExtendedSpendingKey accountSpendingKey = zip32.CreateSaplingAccount(0);
		DiversifierIndex diversifierIndex = default;
		Assert.True(accountSpendingKey.IncomingViewingKey.TryCreateReceiver(ref diversifierIndex, out SaplingReceiver? receiver));
		Assert.Equal(3, diversifierIndex.ToBigInteger()); // indexes 0-2 were invalid in this case.
		SaplingAddress address = new(receiver.Value);
		this.logger.WriteLine(address);
		Assert.Equal("zs16jqxx7r4kqp2k7w95ul27u0dxqggmm3h4e9ng2m7jvfn9809jwjmdhg7wskeypjtw3pmzlr5flt", address.Address);
	}

	[Fact]
	public void CreateTransparentAddressFromSeed()
	{
		Zip32HDWallet zip32 = new(Mnemonic, ZcashNetwork.MainNet);
		Zip32HDWallet.Transparent.ExtendedSpendingKey accountSpendingKey = zip32.CreateTransparentAccount(0);
		Assert.Equal("t1ULaxNrHTCgqrzQsmNMKQUCsfGF9iaHwJv", accountSpendingKey.FullViewingKey.DefaultAddress);
	}

	[Fact]
	public void Orchard_Create_SeedLengthRequirements()
	{
		Assert.Throws<ArgumentException>(() => Zip32HDWallet.Orchard.Create(new byte[31], ZcashNetwork.MainNet));
		Assert.Throws<ArgumentException>(() => Zip32HDWallet.Orchard.Create(new byte[253], ZcashNetwork.MainNet));
	}

	[Fact]
	public void Sapling_Create_SeedLengthRequirements()
	{
		Assert.Throws<ArgumentException>(() => Zip32HDWallet.Sapling.Create(new byte[31], ZcashNetwork.MainNet));
		Assert.Throws<ArgumentException>(() => Zip32HDWallet.Sapling.Create(new byte[253], ZcashNetwork.MainNet));
	}

	[Fact]
	public void Zip32_Ctor_SeedPhraseLengthRequirements()
	{
		Bip39Mnemonic shortMnemonic = Bip39Mnemonic.Create(Zip32HDWallet.MinimumEntropyLengthInBits - 32);
		ArgumentException ex = Assert.Throws<ArgumentException>(() => new Zip32HDWallet(shortMnemonic, ZcashNetwork.MainNet));
		this.logger.WriteLine(ex.Message);
	}

	[Fact]
	public void Orchard_Create_SeedPhraseLengthRequirements()
	{
		Bip39Mnemonic shortMnemonic = Bip39Mnemonic.Create(Zip32HDWallet.MinimumEntropyLengthInBits - 32);
		ArgumentException ex = Assert.Throws<ArgumentException>(() => Zip32HDWallet.Orchard.Create(shortMnemonic, ZcashNetwork.MainNet));
		this.logger.WriteLine(ex.Message);
	}

	[Fact]
	public void Sapling_Create_SeedPhraseLengthRequirements()
	{
		Bip39Mnemonic shortMnemonic = Bip39Mnemonic.Create(Zip32HDWallet.MinimumEntropyLengthInBits - 32);
		ArgumentException ex = Assert.Throws<ArgumentException>(() => Zip32HDWallet.Sapling.Create(shortMnemonic, ZcashNetwork.MainNet));
		this.logger.WriteLine(ex.Message);
	}

	[Fact]
	public void DeriveSaplingInternalSpendingKey()
	{
		Zip32HDWallet.Sapling.ExtendedSpendingKey sk = Zip32HDWallet.Sapling.Create(Mnemonic, ZcashNetwork.MainNet);
		this.logger.WriteLine($"Public address: {sk.DefaultAddress}");
		Zip32HDWallet.Sapling.ExtendedSpendingKey internalSk = sk.DeriveInternal();
		this.logger.WriteLine($"Internal address: {internalSk.DefaultAddress}");
		Assert.Equal("zs192frvl4cfusulnkcvg32z24phrx9kl3e8c58tzytnpj9er704ynkrq5zd8lg3pelnqujjs332mq", internalSk.DefaultAddress);
		Assert.Equal(internalSk.DefaultAddress, internalSk.ExtendedFullViewingKey.DefaultAddress);
	}

	[Fact]
	public void DeriveSaplingInternalFullViewingKey()
	{
		Zip32HDWallet.Sapling.ExtendedFullViewingKey fvk = Zip32HDWallet.Sapling.Create(Mnemonic, ZcashNetwork.MainNet).ExtendedFullViewingKey;
		this.logger.WriteLine($"Public address: {fvk.DefaultAddress}");
		Zip32HDWallet.Sapling.ExtendedFullViewingKey internalSk = fvk.DeriveInternal();
		this.logger.WriteLine($"Internal address: {internalSk.DefaultAddress}");
		Assert.Equal("zs192frvl4cfusulnkcvg32z24phrx9kl3e8c58tzytnpj9er704ynkrq5zd8lg3pelnqujjs332mq", internalSk.DefaultAddress);
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
		Assert.Equal(1u | Bip32KeyPath.HardenedBit, account.ChildIndex);

		Assert.Equal(new OrchardAddress(account.IncomingViewingKey.CreateReceiver(0), ZcashNetwork.TestNet), account.DefaultAddress);
	}

	[Fact]
	public void CreateSaplingAccount()
	{
		Zip32HDWallet wallet = new(Mnemonic, ZcashNetwork.TestNet);
		Zip32HDWallet.Sapling.ExtendedSpendingKey account = wallet.CreateSaplingAccount(1);
		Assert.Equal(1u | Bip32KeyPath.HardenedBit, account.ChildIndex);

		DiversifierIndex diversifier = default;
		Assert.True(account.IncomingViewingKey.TryCreateReceiver(ref diversifier, out SaplingReceiver? receiver));
		Assert.Equal(new SaplingAddress(receiver.Value, ZcashNetwork.TestNet), account.DefaultAddress);
	}

	[Theory, PairwiseData]
	public void ExtendedSpendingKey_Sapling_TextEncoding_TryDecode(bool testNet)
	{
		ZcashNetwork network = testNet ? ZcashNetwork.TestNet : ZcashNetwork.MainNet;
		string expected = testNet
			? "secret-extended-key-test1qwgr6ehwqqqqpq8wxv4wuwhqzlfgam673vzk8eyq0s85t7ny3yszkrnqmmjg8yhk3gqlcuz7w3hfag3fpx3jkfcav3fkmgltkcxatu4jkxurvpg4fths2xszggrem7e5l3j0tej7tq5rce7kd4rcqs9ls6gpthjkanzfevspaw2t56wcan7m3el97m22jj3u7s3qxdnw5qkm3tgm5878yn3zth7wrh99ecslcxm4n4vqm2jjusns4gu2c6kh3qwly625lvmyyaw5lag35crc3"
			: "secret-extended-key-main1qveachkgqqqqpq99ju5nxh8zx5k5kkaeante0l3zcv0737su6jyadm4kk337qeu7h688q3lldl4y576up9eejguqgy0s8jxsfz70ycjhg09qhanqw6dqfs9pe4k7arr97uve2mar8yjah8tqhfad924xr72mrqjn6t69fds8kuwwm9dzxngcurfzgnqtpx4mvj7z8dpewx7edey0yaaatjnhjdnan4vqxvmny003n4l2ye9ey5nt5y3sqfy3r6l0ungptk2u2qgaxschwsjly";
		Zip32HDWallet wallet = new(Mnemonic, network);
		Zip32HDWallet.Sapling.ExtendedSpendingKey account = wallet.CreateSaplingAccount(0);
		string actual = account.TextEncoding;
		this.logger.WriteLine(actual);
		Assert.Equal(expected, actual);

		Assert.True(Zip32HDWallet.Sapling.ExtendedSpendingKey.TryDecode(actual, out _, out _, out Zip32HDWallet.Sapling.ExtendedSpendingKey? decoded));
		Assert.Equal(account, decoded);
	}

	[Theory, PairwiseData]
	public void ExtendedSpendingKey_Orchard_TextEncoding_TryDecode(bool testNet)
	{
		ZcashNetwork network = testNet ? ZcashNetwork.TestNet : ZcashNetwork.MainNet;
		string expected = testNet
			? "secret-orchard-extsk-test1qvduccsrqqqqpq8udd50xc8enfm5gxrsv4n0jz67u9xdlwde6w5m6kgqtgj3d8nfwl2pc8jwuhg36ev0h0w49vkpwylegjumj2wxt6ht5q4nze0e6qkhx80usss"
			: "secret-orchard-extsk-main1qv7pv968qqqqpq8ack7lxnahrll9p42u6twtl8k967nndl8zc77x0um59ln8p2tae8600us9e8hxj4mnvzhatwatzssxa82vkx6j9kqumsv3p756q22jx99sa0t";
		Zip32HDWallet wallet = new(Mnemonic, network);
		Zip32HDWallet.Orchard.ExtendedSpendingKey account = wallet.CreateOrchardAccount(0);
		string actual = account.TextEncoding;
		this.logger.WriteLine(actual);
		Assert.Equal(expected, actual);

		Assert.True(Zip32HDWallet.Orchard.ExtendedSpendingKey.TryDecode(actual, out _, out _, out Zip32HDWallet.Orchard.ExtendedSpendingKey? decoded));
		Assert.Equal(account, decoded);
	}

	[Fact]
	public void Equals_True()
	{
		Zip32HDWallet wallet1 = new(Mnemonic, ZcashNetwork.TestNet);
		Zip32HDWallet wallet2 = new(Mnemonic, ZcashNetwork.TestNet);
		Assert.Equal(wallet1, wallet2);

		// Test explicitly with the object.Equals override.
		Assert.True(wallet1.Equals((object?)wallet2));

		// Test GetHashCode too.
		Assert.Equal(wallet1.GetHashCode(), wallet2.GetHashCode());
	}

	[Fact]
	public void Equals_DifferentNetwork()
	{
		Zip32HDWallet wallet1 = new(Mnemonic, ZcashNetwork.TestNet);
		Zip32HDWallet wallet2 = new(Mnemonic, ZcashNetwork.MainNet);
		Assert.NotEqual(wallet1, wallet2);

		// Test explicitly with the object.Equals override.
		Assert.False(wallet1.Equals((object?)wallet2));

		// Test GetHashCode too.
		Assert.NotEqual(wallet1.GetHashCode(), wallet2.GetHashCode());
	}

	[Fact]
	public void Equals_MnemonicVsEquivalentSeed()
	{
		Zip32HDWallet wallet1 = new(Mnemonic, ZcashNetwork.TestNet);
		Zip32HDWallet wallet2 = new(Mnemonic.Seed, ZcashNetwork.TestNet);
		Assert.Equal(wallet1, wallet2);

		// Test GetHashCode too.
		Assert.Equal(wallet1.GetHashCode(), wallet2.GetHashCode());
	}

	[Fact]
	public void Equals_DifferentSeed()
	{
		Zip32HDWallet wallet1 = new(Mnemonic, ZcashNetwork.TestNet);
		Zip32HDWallet wallet2 = new(Bip39Mnemonic.Create(Zip32HDWallet.MinimumEntropyLengthInBits), ZcashNetwork.TestNet);
		Assert.NotEqual(wallet1, wallet2);

		// Test explicitly with the object.Equals override.
		Assert.False(wallet1.Equals((object?)wallet2));

		// Test GetHashCode too.
		Assert.NotEqual(wallet1.GetHashCode(), wallet2.GetHashCode());
	}
}
