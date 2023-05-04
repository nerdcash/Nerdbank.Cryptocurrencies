// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public class Bip32HDWalletTests
{
	private readonly ITestOutputHelper logger;

	public Bip32HDWalletTests(ITestOutputHelper logger)
	{
		this.logger = logger;
	}

	[Fact]
	public void Create()
	{
		var mnemonic = Bip39Mnemonic.Parse("diary slender airport");
		this.logger.WriteLine(mnemonic.SeedPhrase);

		var extKey = Bip32HDWallet.ExtKey.Create(mnemonic);
		string actual = extKey.ToString();

		string expected = "xprv9s21ZrQH143K3YEex7MKRytjL5o6c8T9jZ8MUQPftJHJDAcitqjWTTiXh6sGNBdLhRiMNwMNQvGVu86qFVzCSKtRqNqBsppThKWnQ6GXJxW";
		this.logger.WriteLine($"EXPECTED: {expected}");
		this.logger.WriteLine($"ACTUAL:   {actual}");
		Assert.Equal(expected, actual);
	}
}
