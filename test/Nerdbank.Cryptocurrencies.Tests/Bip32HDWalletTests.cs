// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Cryptocurrencies;
using static Nerdbank.Cryptocurrencies.Bip32HDWallet;

public class Bip32HDWalletTests
{
	private static readonly TestVector TestVector1 = new(
		"000102030405060708090a0b0c0d0e0f",
		new TestVectorStep[]
		{
			new(
				0, // unused
				"xpub661MyMwAqRbcFtXgS5sYJABqqG9YLmC4Q1Rdap9gSE8NqtwybGhePY2gZ29ESFjqJoCu1Rupje8YtGqsefD265TMg7usUDFdp6W1EGMcet8",
				"xprv9s21ZrQH143K3QTDL4LXw2F7HEK3wJUD2nW2nRk4stbPy6cq3jPPqjiChkVvvNKmPGJxWUtg6LnF5kejMRNNU3TGtRBeJgk33yuGBxrMPHi"),
			new(
				0 | KeyPath.HardenedBit,
				"xpub68Gmy5EdvgibQVfPdqkBBCHxA5htiqg55crXYuXoQRKfDBFA1WEjWgP6LHhwBZeNK1VTsfTFUHCdrfp1bgwQ9xv5ski8PX9rL2dZXvgGDnw",
				"xprv9uHRZZhk6KAJC1avXpDAp4MDc3sQKNxDiPvvkX8Br5ngLNv1TxvUxt4cV1rGL5hj6KCesnDYUhd7oWgT11eZG7XnxHrnYeSvkzY7d2bhkJ7"),
			new(
				1,
				"xpub6ASuArnXKPbfEwhqN6e3mwBcDTgzisQN1wXN9BJcM47sSikHjJf3UFHKkNAWbWMiGj7Wf5uMash7SyYq527Hqck2AxYysAA7xmALppuCkwQ",
				"xprv9wTYmMFdV23N2TdNG573QoEsfRrWKQgWeibmLntzniatZvR9BmLnvSxqu53Kw1UmYPxLgboyZQaXwTCg8MSY3H2EU4pWcQDnRnrVA1xe8fs"),
			new(
				2 | KeyPath.HardenedBit,
				"xpub6D4BDPcP2GT577Vvch3R8wDkScZWzQzMMUm3PWbmWvVJrZwQY4VUNgqFJPMM3No2dFDFGTsxxpG5uJh7n7epu4trkrX7x7DogT5Uv6fcLW5",
				"xprv9z4pot5VBttmtdRTWfWQmoH1taj2axGVzFqSb8C9xaxKymcFzXBDptWmT7FwuEzG3ryjH4ktypQSAewRiNMjANTtpgP4mLTj34bhnZX7UiM"),
			new(
				2,
				"xpub6FHa3pjLCk84BayeJxFW2SP4XRrFd1JYnxeLeU8EqN3vDfZmbqBqaGJAyiLjTAwm6ZLRQUMv1ZACTj37sR62cfN7fe5JnJ7dh8zL4fiyLHV",
				"xprvA2JDeKCSNNZky6uBCviVfJSKyQ1mDYahRjijr5idH2WwLsEd4Hsb2Tyh8RfQMuPh7f7RtyzTtdrbdqqsunu5Mm3wDvUAKRHSC34sJ7in334"),
			new(
				1000000000,
				"xpub6H1LXWLaKsWFhvm6RVpEL9P4KfRZSW7abD2ttkWP3SSQvnyA8FSVqNTEcYFgJS2UaFcxupHiYkro49S8yGasTvXEYBVPamhGW6cFJodrTHy",
				"xprvA41z7zogVVwxVSgdKUHDy1SKmdb533PjDz7J6N6mV6uS3ze1ai8FHa8kmHScGpWmj4WggLyQjgPie1rFSruoUihUZREPSL39UNdE3BBDu76"),
		});

	private static readonly TestVector TestVector2 = new(
		"fffcf9f6f3f0edeae7e4e1dedbd8d5d2cfccc9c6c3c0bdbab7b4b1aeaba8a5a29f9c999693908d8a8784817e7b7875726f6c696663605d5a5754514e4b484542",
		new TestVectorStep[]
		{
			new(
				0,
				"xpub661MyMwAqRbcFW31YEwpkMuc5THy2PSt5bDMsktWQcFF8syAmRUapSCGu8ED9W6oDMSgv6Zz8idoc4a6mr8BDzTJY47LJhkJ8UB7WEGuduB",
				"xprv9s21ZrQH143K31xYSDQpPDxsXRTUcvj2iNHm5NUtrGiGG5e2DtALGdso3pGz6ssrdK4PFmM8NSpSBHNqPqm55Qn3LqFtT2emdEXVYsCzC2U"),
			new(
				0,
				"xpub69H7F5d8KSRgmmdJg2KhpAK8SR3DjMwAdkxj3ZuxV27CprR9LgpeyGmXUbC6wb7ERfvrnKZjXoUmmDznezpbZb7ap6r1D3tgFxHmwMkQTPH",
				"xprv9vHkqa6EV4sPZHYqZznhT2NPtPCjKuDKGY38FBWLvgaDx45zo9WQRUT3dKYnjwih2yJD9mkrocEZXo1ex8G81dwSM1fwqWpWkeS3v86pgKt"),
			new(
				2147483647 | KeyPath.HardenedBit,
				"xpub6ASAVgeehLbnwdqV6UKMHVzgqAG8Gr6riv3Fxxpj8ksbH9ebxaEyBLZ85ySDhKiLDBrQSARLq1uNRts8RuJiHjaDMBU4Zn9h8LZNnBC5y4a",
				"xprv9wSp6B7kry3Vj9m1zSnLvN3xH8RdsPP1Mh7fAaR7aRLcQMKTR2vidYEeEg2mUCTAwCd6vnxVrcjfy2kRgVsFawNzmjuHc2YmYRmagcEPdU9"),
			new(
				1,
				"xpub6DF8uhdarytz3FWdA8TvFSvvAh8dP3283MY7p2V4SeE2wyWmG5mg5EwVvmdMVCQcoNJxGoWaU9DCWh89LojfZ537wTfunKau47EL2dhHKon",
				"xprv9zFnWC6h2cLgpmSA46vutJzBcfJ8yaJGg8cX1e5StJh45BBciYTRXSd25UEPVuesF9yog62tGAQtHjXajPPdbRCHuWS6T8XA2ECKADdw4Ef"),
			new(
				2147483646 | KeyPath.HardenedBit,
				"xpub6ERApfZwUNrhLCkDtcHTcxd75RbzS1ed54G1LkBUHQVHQKqhMkhgbmJbZRkrgZw4koxb5JaHWkY4ALHY2grBGRjaDMzQLcgJvLJuZZvRcEL",
				"xprvA1RpRA33e1JQ7ifknakTFpgNXPmW2YvmhqLQYMmrj4xJXXWYpDPS3xz7iAxn8L39njGVyuoseXzU6rcxFLJ8HFsTjSyQbLYnMpCqE2VbFWc"),
			new(
				2,
				"xpub6FnCn6nSzZAw5Tw7cgR9bi15UV96gLZhjDstkXXxvCLsUXBGXPdSnLFbdpq8p9HmGsApME5hQTZ3emM2rnY5agb9rXpVGyy3bdW6EEgAtqt",
				"xprvA2nrNbFZABcdryreWet9Ea4LvTJcGsqrMzxHx98MMrotbir7yrKCEXw7nadnHM8Dq38EGfSh6dqA9QWTyefMLEcBYJUuekgW4BYPJcr9E7j"),
		});

	/// <summary>
	/// These vectors test for the retention of leading zeros. See <see href="https://github.com/bitpay/bitcore-lib/issues/47">bitpay/bitcore-lib#47</see> and <see href="https://github.com/iancoleman/bip39/issues/58">iancoleman/bip39#58</see> for more information.
	/// </summary>
	private static readonly TestVector TestVector3 = new(
		"4b381541583be4423346c643850da4b320e46a87ae3d2a4e6da11eba819cd4acba45d239319ac14f863b8d5ab5a0d0c64d2e8a1e7d1457df2e5a3c51c73235be",
		new TestVectorStep[]
		{
			new(
				0,
				"xpub661MyMwAqRbcEZVB4dScxMAdx6d4nFc9nvyvH3v4gJL378CSRZiYmhRoP7mBy6gSPSCYk6SzXPTf3ND1cZAceL7SfJ1Z3GC8vBgp2epUt13",
				"xprv9s21ZrQH143K25QhxbucbDDuQ4naNntJRi4KUfWT7xo4EKsHt2QJDu7KXp1A3u7Bi1j8ph3EGsZ9Xvz9dGuVrtHHs7pXeTzjuxBrCmmhgC6"),
			new(
				0 | KeyPath.HardenedBit,
				"xpub68NZiKmJWnxxS6aaHmn81bvJeTESw724CRDs6HbuccFQN9Ku14VQrADWgqbhhTHBaohPX4CjNLf9fq9MYo6oDaPPLPxSb7gwQN3ih19Zm4Y",
				"xprv9uPDJpEQgRQfDcW7BkF7eTya6RPxXeJCqCJGHuCJ4GiRVLzkTXBAJMu2qaMWPrS7AANYqdq6vcBcBUdJCVVFceUvJFjaPdGZ2y9WACViL4L"),
		});

	/// <summary>
	/// These vectors test for the retention of leading zeros. See <see href="https://github.com/btcsuite/btcutil/issues/172">btcsuite/btcutil#172</see> for more information.
	/// </summary>
	private static readonly TestVector TestVector4 = new(
		"3ddd5602285899a946114506157c7997e5444528f3003f6134712147db19b678",
		new TestVectorStep[]
		{
			new(
				0,
				"xpub661MyMwAqRbcGczjuMoRm6dXaLDEhW1u34gKenbeYqAix21mdUKJyuyu5F1rzYGVxyL6tmgBUAEPrEz92mBXjByMRiJdba9wpnN37RLLAXa",
				"xprv9s21ZrQH143K48vGoLGRPxgo2JNkJ3J3fqkirQC2zVdk5Dgd5w14S7fRDyHH4dWNHUgkvsvNDCkvAwcSHNAQwhwgNMgZhLtQC63zxwhQmRv"),
			new(
				0 | KeyPath.HardenedBit,
				"xpub69AUMk3qDBi3uW1sXgjCmVjJ2G6WQoYSnNHyzkmdCHEhSZ4tBok37xfFEqHd2AddP56Tqp4o56AePAgCjYdvpW2PU2jbUPFKsav5ut6Ch1m",
				"xprv9vB7xEWwNp9kh1wQRfCCQMnZUEG21LpbR9NPCNN1dwhiZkjjeGRnaALmPXCX7SgjFTiCTT6bXes17boXtjq3xLpcDjzEuGLQBM5ohqkao9G"),
			new(
				1 | KeyPath.HardenedBit,
				"xpub6BJA1jSqiukeaesWfxe6sNK9CCGaujFFSJLomWHprUL9DePQ4JDkM5d88n49sMGJxrhpjazuXYWdMf17C9T5XnxkopaeS7jGk1GyyVziaMt",
				"xprv9xJocDuwtYCMNAo3Zw76WENQeAS6WGXQ55RCy7tDJ8oALr4FWkuVoHJeHVAcAqiZLE7Je3vZJHxspZdFHfnBEjHqU5hG1Jaj32dVoS6XLT1"),
		});

	private readonly ITestOutputHelper logger;

	public Bip32HDWalletTests(ITestOutputHelper logger)
	{
		this.logger = logger;
	}

	public static object[][] TestVectors => new object[][]
	{
		new object[] { TestVector1 },
		new object[] { TestVector2 },
		new object[] { TestVector3 },
		new object[] { TestVector4 },
	};

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

	[Theory]
	[InlineData("xpub661MyMwAqRbcEYS8w7XLSVeEsBXy79zSzH1J8vCdxAZningWLdN3zgtU6LBpB85b3D2yc8sfvZU521AAwdZafEz7mnzBBsz4wKY5fTtTQBm", DecodeError.InvalidKey)] // pubkey version / prvkey mismatch
	[InlineData("xprv9s21ZrQH143K24Mfq5zL5MhWK9hUhhGbd45hLXo2Pq2oqzMMo63oStZzFGTQQD3dC4H2D5GBj7vWvSQaaBv5cxi9gafk7NF3pnBju6dwKvH", DecodeError.InvalidKey)] // prvkey version / pubkey mismatch
	[InlineData("xpub661MyMwAqRbcEYS8w7XLSVeEsBXy79zSzH1J8vCdxAZningWLdN3zgtU6Txnt3siSujt9RCVYsx4qHZGc62TG4McvMGcAUjeuwZdduYEvFn", DecodeError.InvalidKey)] // invalid pubkey prefix 04
	[InlineData("xprv9s21ZrQH143K24Mfq5zL5MhWK9hUhhGbd45hLXo2Pq2oqzMMo63oStZzFGpWnsj83BHtEy5Zt8CcDr1UiRXuWCmTQLxEK9vbz5gPstX92JQ", DecodeError.InvalidKey)] // invalid prvkey prefix 04
	[InlineData("xpub661MyMwAqRbcEYS8w7XLSVeEsBXy79zSzH1J8vCdxAZningWLdN3zgtU6N8ZMMXctdiCjxTNq964yKkwrkBJJwpzZS4HS2fxvyYUA4q2Xe4", DecodeError.InvalidKey)] // invalid pubkey prefix 01
	[InlineData("xprv9s21ZrQH143K24Mfq5zL5MhWK9hUhhGbd45hLXo2Pq2oqzMMo63oStZzFAzHGBP2UuGCqWLTAPLcMtD9y5gkZ6Eq3Rjuahrv17fEQ3Qen6J", DecodeError.InvalidKey)] // invalid prvkey prefix 01
	[InlineData("xprv9s2SPatNQ9Vc6GTbVMFPFo7jsaZySyzk7L8n2uqKXJen3KUmvQNTuLh3fhZMBoG3G4ZW1N2kZuHEPY53qmbZzCHshoQnNf4GvELZfqTUrcv", DecodeError.InvalidDerivationData)] // zero depth with non-zero parent fingerprint
	[InlineData("xpub661no6RGEX3uJkY4bNnPcw4URcQTrSibUZ4NqJEw5eBkv7ovTwgiT91XX27VbEXGENhYRCf7hyEbWrR3FewATdCEebj6znwMfQkhRYHRLpJ", DecodeError.InvalidDerivationData)] // zero depth with non-zero parent fingerprint
	[InlineData("xprv9s21ZrQH4r4TsiLvyLXqM9P7k1K3EYhA1kkD6xuquB5i39AU8KF42acDyL3qsDbU9NmZn6MsGSUYZEsuoePmjzsB3eFKSUEh3Gu1N3cqVUN", DecodeError.InvalidDerivationData)] // zero depth with non-zero index
	[InlineData("xpub661MyMwAuDcm6CRQ5N4qiHKrJ39Xe1R1NyfouMKTTWcguwVcfrZJaNvhpebzGerh7gucBvzEQWRugZDuDXjNDRmXzSZe4c7mnTK97pTvGS8", DecodeError.InvalidDerivationData)] // zero depth with non-zero index
	[InlineData("DMwo58pR1QLEFihHiXPVykYB6fJmsTeHvyTp7hRThAtCX8CvYzgPcn8XnmdfHGMQzT7ayAmfo4z3gY5KfbrZWZ6St24UVf2Qgo6oujFktLHdHY4", DecodeError.UnrecognizedVersion)] // unknown extended key version
	[InlineData("DMwo58pR1QLEFihHiXPVykYB6fJmsTeHvyTp7hRThAtCX8CvYzgPcn8XnmdfHPmHJiEDXkTiJTVV9rHEBUem2mwVbbNfvT2MTcAqj3nesx8uBf9", DecodeError.UnrecognizedVersion)] // unknown extended key version
	[InlineData("xprv9s21ZrQH143K24Mfq5zL5MhWK9hUhhGbd45hLXo2Pq2oqzMMo63oStZzF93Y5wvzdUayhgkkFoicQZcP3y52uPPxFnfoLZB21Teqt1VvEHx", DecodeError.InvalidKey)] // private key 0 not in 1..n-1
	[InlineData("xprv9s21ZrQH143K24Mfq5zL5MhWK9hUhhGbd45hLXo2Pq2oqzMMo63oStZzFAzHGBP2UuGCqWLTAPLcMtD5SDKr24z3aiUvKr9bJpdrcLg1y3G", DecodeError.InvalidKey)] // private key n not in 1..n-1
	[InlineData("xpub661MyMwAqRbcEYS8w7XLSVeEsBXy79zSzH1J8vCdxAZningWLdN3zgtU6Q5JXayek4PRsn35jii4veMimro1xefsM58PgBMrvdYre8QyULY", DecodeError.InvalidKey)] // invalid pubkey 020000000000000000000000000000000000000000000000000000000000000007
	[InlineData("xprv9s21ZrQH143K3QTDL4LXw2F7HEK3wJUD2nW2nRk4stbPy6cq3jPPqjiChkVvvNKmPGJxWUtg6LnF5kejMRNNU3TGtRBeJgk33yuGBxrMPHL", DecodeError.InvalidChecksum)] // invalid checksum
	public void ExtendedKey_TryParse(string base58Encoded, DecodeError expectedDecodeError)
	{
		this.logger.WriteLine($"Decoding {base58Encoded}");
		Assert.False(ExtendedKeyBase.TryParse(base58Encoded, out _, out DecodeError? decodeError, out string? errorMessage));
		this.logger.WriteLine($"DecodeError {decodeError}: {errorMessage}");
		Assert.Equal(expectedDecodeError, decodeError.Value);
	}

	[Fact]
	public void ExtendedKeyBase_TryParse_Empty()
	{
		Assert.False(ExtendedKeyBase.TryParse(string.Empty, out _, out DecodeError? decodeError, out string? errorMessage));
		this.logger.WriteLine($"DecodeError {decodeError}: {errorMessage}");
	}

	[Fact]
	public void ExtendedPrivateKey_Parse_Roundtripping()
	{
		using ExtendedPrivateKey pvk = ExtendedPrivateKey.Create(Bip39Mnemonic.Create(128));
		string pvkAsString = pvk.ToString();
		using ExtendedPrivateKey pvk2 = Assert.IsType<ExtendedPrivateKey>(ExtendedKeyBase.Parse(pvk.ToString()));
		AssertEqual(pvkAsString, pvk2);

		// TODO: test parsing derived keys.
	}

	[Fact]
	public void ExtendedPublicKey_Parse_Roundtripping()
	{
		using ExtendedPrivateKey pvk = ExtendedPrivateKey.Create(Bip39Mnemonic.Create(128));
		ExtendedPublicKey pub = pvk.PublicKey;
		string pubAsString = pub.ToString();
		ExtendedPublicKey pub2 = Assert.IsType<ExtendedPublicKey>(ExtendedKeyBase.Parse(pub.ToString()));
		AssertEqual(pubAsString, pub2);

		// TODO: test parsing derived keys.
	}

	private static void AssertEqual(string expectedBase58Encoding, ExtendedKeyBase actual)
	{
		Assert.Equal(Base58ToHex(expectedBase58Encoding), Base58ToHex(actual.ToString()));
	}

	public record TestVectorStep(uint ChildNumber, string EncodedPublicKey, string EncodedPrivateKey);

	public record TestVector(string SeedAsHex, params TestVectorStep[] Steps);
}
