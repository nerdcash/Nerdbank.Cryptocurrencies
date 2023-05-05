// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using static Nerdbank.Cryptocurrencies.Bip32HDWallet;

public class Bip32HDWalletTests
{
	private readonly ITestOutputHelper logger;

	public Bip32HDWalletTests(ITestOutputHelper logger)
	{
		this.logger = logger;
	}

	[Fact]
	public void ExtendedPrivateKey_Create()
	{
		var mnemonic = Bip39Mnemonic.Parse("diary slender airport");
		this.logger.WriteLine(mnemonic.SeedPhrase);

		using var extKey = ExtendedPrivateKey.Create(mnemonic);
		string actual = extKey.ToString();

		string expected = "xprv9s21ZrQH143K3YEex7MKRytjL5o6c8T9jZ8MUQPftJHJDAcitqjWTTiXh6sGNBdLhRiMNwMNQvGVu86qFVzCSKtRqNqBsppThKWnQ6GXJxW";
		this.logger.WriteLine($"EXPECTED: {expected}");
		this.logger.WriteLine($"ACTUAL:   {actual}");
		Assert.Equal(expected, actual);
	}

	[Fact]
	public void ExtendedPrivateKey_PublicKey()
	{
		var mnemonic = Bip39Mnemonic.Parse("diary slender airport");
		this.logger.WriteLine(mnemonic.SeedPhrase);

		using var privateKey = ExtendedPrivateKey.Create(mnemonic);
		string actual = privateKey.PublicKey.ToString();

		string expected = "xpub661MyMwAqRbcG2K848tKo7qTt7db1bB16n3xGnoHSdpH5xwsSP3m1G31YMDw3C5bugL4CxHaCPKTvj1rFi8aFaY9zGT8E1GUdFt9Mst2UNu";
		this.logger.WriteLine($"EXPECTED: {expected}");
		this.logger.WriteLine($"ACTUAL:   {actual}");
		Assert.Equal(expected, actual);
	}
}
