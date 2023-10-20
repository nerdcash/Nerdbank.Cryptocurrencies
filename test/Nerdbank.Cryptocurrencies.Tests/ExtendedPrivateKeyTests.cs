// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using static Nerdbank.Cryptocurrencies.Bip32HDWallet;

public class ExtendedPrivateKeyTests : Bip32HDWalletTestBase
{
	public ExtendedPrivateKeyTests(ITestOutputHelper logger)
		: base(logger)
	{
	}

	[Fact]
	public void Create()
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
	public void PublicKey()
	{
		var mnemonic = Bip39Mnemonic.Parse("diary slender airport");
		this.logger.WriteLine(mnemonic.SeedPhrase);

		using var privateKey = ExtendedPrivateKey.Create(mnemonic);
		string actual = privateKey.PublicKey.ToString();

		string expected = "xpub661MyMwAqRbcG2K848tKo7qTt7db1bB16n3xGnoHSdpH5xwsSP3m1G31YMDw3C5bugL4CxHaCPKTvj1rFi8aFaY9zGT8E1GUdFt9Mst2UNu";
		this.logger.WriteLine($"EXPECTED: {expected}");
		this.logger.WriteLine($"ACTUAL:   {actual}");
		Assert.Equal(expected, actual);

		this.logger.WriteLine($"Identifier: {Convert.ToHexString(privateKey.PublicKey.Identifier)}");
	}

	/// <summary>
	/// Asserts matching <see href="https://github.com/bitcoin/bips/blob/master/bip-0032.mediawiki#test-vectors">Test vectors from BIP-32</see>.
	/// </summary>
	/// <param name="vector">The vector to test.</param>
	[Theory, MemberData(nameof(TestVectors))]
	public void Derive_TestVectors(TestVector vector)
	{
		ExtendedPrivateKey current = ExtendedPrivateKey.Create(Convert.FromHexString(vector.SeedAsHex));
		KeyPath keyPath = KeyPath.Root;
		this.logger.WriteLine($"Initial state: {keyPath}");
		TestVectorStep step = vector.Steps[0];
		AssertMatch();

		for (int i = 1; i < vector.Steps.Length; i++)
		{
			step = vector.Steps[i];
			keyPath = new KeyPath(step.ChildIndex, keyPath);
			this.logger.WriteLine($"Step {i}: {keyPath}");
			current = current.Derive(step.ChildIndex);
			AssertMatch();
		}

		void AssertMatch()
		{
			AssertEqual(step.EncodedPublicKey, current.PublicKey);
			AssertEqual(step.EncodedPrivateKey, current);
		}
	}

	[Fact]
	public void Derive_KeyPath_FromMaster()
	{
		using ExtendedPrivateKey master = ExtendedPrivateKey.Create(Bip39Mnemonic.Create(32));
		string expected = master.Derive(1).Derive(2).Derive(3).ToString();

		using ExtendedPrivateKey derive12 = master.Derive(KeyPath.Parse("/1/2"));
		using ExtendedPrivateKey derive3 = derive12.Derive(KeyPath.Parse("/3"));
		AssertEqual(expected, derive3);

		using ExtendedPrivateKey derive123Unrooted = master.Derive(KeyPath.Parse("/1/2/3"));
		using ExtendedPrivateKey derive123Rooted = master.Derive(KeyPath.Parse("m/1/2/3"));
		AssertEqual(expected, derive123Unrooted);
		AssertEqual(expected, derive123Rooted);
	}

	[Fact]
	public void Derive_KeyPath_RootedOnNonMaster()
	{
		using ExtendedPrivateKey master = ExtendedPrivateKey.Create(Bip39Mnemonic.Create(32));
		using ExtendedPrivateKey derived = master.Derive(1);

		// This first simply cannot happen because one cannot derive a sibling key.
		Assert.Throws<NotSupportedException>(() => derived.Derive(KeyPath.Parse("m/2")));

		// This cannot happen until we decide that since we can match the depth and child number,
		// we can *assume* the rest of the path is the same and proceed to derive the next step.
		Assert.Throws<NotSupportedException>(() => derived.Derive(KeyPath.Parse("m/1/2")));
	}

	[Fact]
	public void Decode_Roundtripping()
	{
		using ExtendedPrivateKey pvk = ExtendedPrivateKey.Create(Bip39Mnemonic.Create(128));
		string pvkAsString = pvk.ToString();
		using ExtendedPrivateKey pvk2 = Assert.IsType<ExtendedPrivateKey>(ExtendedKeyBase.Decode(pvk.ToString()));
		AssertEqual(pvkAsString, pvk2);

		// Test parsing of derived keys.
		using ExtendedPrivateKey pvkDerived = pvk.Derive(1);
		string pvkDerivedAsString = pvkDerived.ToString();
		using ExtendedPrivateKey pvkDerived2 = Assert.IsType<ExtendedPrivateKey>(ExtendedKeyBase.Decode(pvkDerived.ToString()));
		AssertEqual(pvkDerivedAsString, pvkDerived2);
	}

	[Fact]
	public void DerivationPath()
	{
		using ExtendedPrivateKey master = ExtendedPrivateKey.Create(Bip39Mnemonic.Create(32));
		Assert.Equal(KeyPath.Root, master.DerivationPath);
		Assert.Equal("m/2", master.Derive(2).DerivationPath?.ToString());
	}
}
