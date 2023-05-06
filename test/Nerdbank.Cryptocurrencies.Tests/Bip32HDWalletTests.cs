// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using static Nerdbank.Cryptocurrencies.Bip32HDWallet;

public class Bip32HDWalletTests
{
	private static readonly TestVector TestVector1 = new(
		"000102030405060708090a0b0c0d0e0f",
		new TestVectorStep[]
		{
			new TestVectorStep(
				0, // unused
				"xpub661MyMwAqRbcFtXgS5sYJABqqG9YLmC4Q1Rdap9gSE8NqtwybGhePY2gZ29ESFjqJoCu1Rupje8YtGqsefD265TMg7usUDFdp6W1EGMcet8",
				"xprv9s21ZrQH143K3QTDL4LXw2F7HEK3wJUD2nW2nRk4stbPy6cq3jPPqjiChkVvvNKmPGJxWUtg6LnF5kejMRNNU3TGtRBeJgk33yuGBxrMPHi"),
			new TestVectorStep(
				0 | KeyPath.HardenedBit,
				"xpub68Gmy5EdvgibQVfPdqkBBCHxA5htiqg55crXYuXoQRKfDBFA1WEjWgP6LHhwBZeNK1VTsfTFUHCdrfp1bgwQ9xv5ski8PX9rL2dZXvgGDnw",
				"xprv9uHRZZhk6KAJC1avXpDAp4MDc3sQKNxDiPvvkX8Br5ngLNv1TxvUxt4cV1rGL5hj6KCesnDYUhd7oWgT11eZG7XnxHrnYeSvkzY7d2bhkJ7"),
			new TestVectorStep(
				1,
				"xpub6ASuArnXKPbfEwhqN6e3mwBcDTgzisQN1wXN9BJcM47sSikHjJf3UFHKkNAWbWMiGj7Wf5uMash7SyYq527Hqck2AxYysAA7xmALppuCkwQ",
				"xprv9wTYmMFdV23N2TdNG573QoEsfRrWKQgWeibmLntzniatZvR9BmLnvSxqu53Kw1UmYPxLgboyZQaXwTCg8MSY3H2EU4pWcQDnRnrVA1xe8fs"),
			new TestVectorStep(
				2 | KeyPath.HardenedBit,
				"xpub6D4BDPcP2GT577Vvch3R8wDkScZWzQzMMUm3PWbmWvVJrZwQY4VUNgqFJPMM3No2dFDFGTsxxpG5uJh7n7epu4trkrX7x7DogT5Uv6fcLW5",
				"xprv9z4pot5VBttmtdRTWfWQmoH1taj2axGVzFqSb8C9xaxKymcFzXBDptWmT7FwuEzG3ryjH4ktypQSAewRiNMjANTtpgP4mLTj34bhnZX7UiM"),
			new TestVectorStep(
				2,
				"xpub6FHa3pjLCk84BayeJxFW2SP4XRrFd1JYnxeLeU8EqN3vDfZmbqBqaGJAyiLjTAwm6ZLRQUMv1ZACTj37sR62cfN7fe5JnJ7dh8zL4fiyLHV",
				"xprvA2JDeKCSNNZky6uBCviVfJSKyQ1mDYahRjijr5idH2WwLsEd4Hsb2Tyh8RfQMuPh7f7RtyzTtdrbdqqsunu5Mm3wDvUAKRHSC34sJ7in334"),
			new TestVectorStep(
				1000000000,
				"xpub6H1LXWLaKsWFhvm6RVpEL9P4KfRZSW7abD2ttkWP3SSQvnyA8FSVqNTEcYFgJS2UaFcxupHiYkro49S8yGasTvXEYBVPamhGW6cFJodrTHy",
				"xprvA41z7zogVVwxVSgdKUHDy1SKmdb533PjDz7J6N6mV6uS3ze1ai8FHa8kmHScGpWmj4WggLyQjgPie1rFSruoUihUZREPSL39UNdE3BBDu76"),
		});

	private readonly ITestOutputHelper logger;

	public Bip32HDWalletTests(ITestOutputHelper logger)
	{
		this.logger = logger;
	}

	public static object[][] TestVectors => new object[][]
	{
		new object[] { TestVector1 },
		//new object[] { TestVector2 },
		//new object[] { TestVector3 },
		//new object[] { TestVector4 },
	};

	// TODO: Add invalid test vectors from the spec and test them.

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

		this.logger.WriteLine($"Identifier: {Convert.ToHexString(privateKey.PublicKey.Identifier)}");
	}

	/// <summary>
	/// Asserts matching <see href="https://github.com/bitcoin/bips/blob/master/bip-0032.mediawiki#test-vectors">Test vectors from BIP-32</see>.
	/// </summary>
	/// <param name="vector">The vector to test.</param>
	[Theory, MemberData(nameof(TestVectors))]
	public void ExtendedPrivateKey_Derive_TestVectors(TestVector vector)
	{
		ExtendedPrivateKey current = ExtendedPrivateKey.Create(Convert.FromHexString(vector.SeedAsHex));
		KeyPath keyPath = KeyPath.Root;
		this.logger.WriteLine($"Initial state: {keyPath}");
		TestVectorStep step = vector.Steps[0];
		AssertMatch();

		for (int i = 1; i < vector.Steps.Length; i++)
		{
			step = vector.Steps[i];
			keyPath = new KeyPath(step.ChildNumber, keyPath);
			this.logger.WriteLine($"Step {i}: {keyPath}");
			current = current.Derive(step.ChildNumber);
			AssertMatch();
		}

		void AssertMatch()
		{
			AssertEqual(step.EncodedPublicKey, current.PublicKey);
			AssertEqual(step.EncodedPrivateKey, current);
		}
	}

	/// <summary>
	/// Asserts matching <see href="https://github.com/bitcoin/bips/blob/master/bip-0032.mediawiki#test-vectors">Test vectors from BIP-32</see>
	/// using the public key as the source of derivation wherever allowed.
	/// </summary>
	/// <param name="vector">The vector to test.</param>
	[Theory, MemberData(nameof(TestVectors))]
	public void ExtendedPublicKey_Derive_TestVectors(TestVector vector)
	{
		ExtendedPrivateKey m = ExtendedPrivateKey.Create(Convert.FromHexString(vector.SeedAsHex));
		ExtendedPublicKey current = m.PublicKey;
		KeyPath keyPath = KeyPath.Root;
		this.logger.WriteLine($"Initial state: {keyPath}");
		TestVectorStep step = vector.Steps[0];
		AssertMatch();

		for (int i = 1; i < vector.Steps.Length; i++)
		{
			step = vector.Steps[i];
			keyPath = new KeyPath(step.ChildNumber, keyPath);
			if (keyPath.IsHardened)
			{
				this.logger.WriteLine($"Step {i}: {keyPath} (skipped due to hardened child)");

				// Continue the derivation by way of the private key so wecan take the next step.
				current = m.Derive(keyPath).PublicKey;
				AssertMatch();
			}
			else
			{
				this.logger.WriteLine($"Step {i}: {keyPath}");
				current = current.Derive(step.ChildNumber);
				AssertMatch();
			}
		}

		void AssertMatch()
		{
			AssertEqual(step.EncodedPublicKey, current);
		}
	}

	private static void AssertEqual(string expectedBase58Encoding, ExtendedKeyBase actual)
	{
		Assert.Equal(Base58ToHex(expectedBase58Encoding), Base58ToHex(actual.ToString()));
	}

	public record TestVectorStep(uint ChildNumber, string EncodedPublicKey, string EncodedPrivateKey);

	public record TestVector(string SeedAsHex, params TestVectorStep[] Steps);
}
