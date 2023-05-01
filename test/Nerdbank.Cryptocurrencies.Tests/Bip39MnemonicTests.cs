// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

public class Bip39MnemonicTests
{
	private readonly ITestOutputHelper logger;

	public Bip39MnemonicTests(ITestOutputHelper logger)
	{
		this.logger = logger;
	}

	[Theory]
	[InlineData(256, 24)]
	[InlineData(128, 12)]
	[InlineData(64, 6)]
	[InlineData(32, 3)]
	public void Generate_Length(int bitLength, int expectedWordCount)
	{
		string seedPhrase = Bip39Mnemonic.Generate(bitLength).SeedPhrase;
		this.logger.WriteLine(seedPhrase);
		Assert.Equal(expectedWordCount, seedPhrase.Split().Length);
	}

	[Fact]
	public void Generate_Length_ProducesUniquePhrases()
	{
		string seedPhrase = Bip39Mnemonic.Generate(64).SeedPhrase;
		string seedPhrase2 = Bip39Mnemonic.Generate(64).SeedPhrase;
		Assert.NotEqual(seedPhrase, seedPhrase2);
	}

	[Fact]
	public void Generate_BadLengths()
	{
		Assert.Throws<ArgumentException>(() => Bip39Mnemonic.Generate(65));
		Assert.Throws<ArgumentException>(() => Bip39Mnemonic.Generate(16));
		Assert.Throws<ArgumentException>(() => Bip39Mnemonic.Generate(0));
	}

	[Fact]
	public void GetEntropyLength()
	{
		StringBuilder seedPhraseBuilder = new();
		for (int wordCount = 3; wordCount <= 24; wordCount += 3)
		{
			seedPhraseBuilder.Append("a a  a ");
			int expectedEntropy = wordCount / 3 * 32;
			int actualEntropy = Bip39Mnemonic.GetEntropyLengthInBits(seedPhraseBuilder.ToString());
			Assert.Equal(expectedEntropy, actualEntropy);
		}
	}

	[Fact]
	public void Generate_Entropy()
	{
		Span<byte> entropy = stackalloc byte[16];
		string seedPhrase = new Bip39Mnemonic(entropy).SeedPhrase;
		this.logger.WriteLine(seedPhrase);
		Assert.Equal("abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about", seedPhrase);

		entropy.Fill(0xff);
		seedPhrase = new Bip39Mnemonic(entropy).SeedPhrase;
		this.logger.WriteLine(seedPhrase);
		Assert.Equal("zoo zoo zoo zoo zoo zoo zoo zoo zoo zoo zoo wrong", seedPhrase);

		RandomNumberGenerator.Fill(entropy);
		seedPhrase = new Bip39Mnemonic(entropy).SeedPhrase;
		this.logger.WriteLine(Convert.ToHexString(entropy));
		this.logger.WriteLine(seedPhrase);
	}

	[Fact]
	public void TryParse_NormalizesCapitalization()
	{
		const string SeedPhrase = "funny essay radar tattoo casual dream idle wrestle defy length obtain tobacco";
		Assert.True(Bip39Mnemonic.TryParse(SeedPhrase.ToUpperInvariant(), password: default, out Bip39Mnemonic? mnemonic, out _, out _));
		Assert.Equal("5E29A6C2EF223A851C2FF239B0026271", Convert.ToHexString(mnemonic.Entropy));
		Assert.Equal(SeedPhrase, mnemonic.SeedPhrase);
	}

	[Fact]
	public void TryParse_WithPassword()
	{
		const string SeedPhrase = "funny essay radar tattoo casual dream idle wrestle defy length obtain tobacco";
		const string Password = "some password";
		Assert.True(Bip39Mnemonic.TryParse(SeedPhrase, Password.AsMemory(), out Bip39Mnemonic? mnemonic, out _, out _));
		Assert.Equal("5E29A6C2EF223A851C2FF239B0026271", Convert.ToHexString(mnemonic.Entropy));
		Assert.Equal(Password.AsMemory(), mnemonic.Password);
		Assert.Equal(SeedPhrase, mnemonic.SeedPhrase);
	}

	[Theory]
	[InlineData("property reward account skull verb cruel false labor parent loop donor mutual adult cheese broom that jelly brass vivid later van people cannon join", "AC771406656F28691493E2A0307D0549103C4E4736FF77C367D4BEAF1345885B")]
	[InlineData("funny essay radar tattoo casual dream idle wrestle defy length obtain tobacco", "5E29A6C2EF223A851C2FF239B0026271")]
	public void TryParse(string seedPhrase, string entropyAsHex)
	{
		Assert.True(Bip39Mnemonic.TryParse(seedPhrase, password: default, out Bip39Mnemonic? mnemonic, out _, out _));
		Assert.Equal(entropyAsHex, Convert.ToHexString(mnemonic.Entropy));
	}
}
