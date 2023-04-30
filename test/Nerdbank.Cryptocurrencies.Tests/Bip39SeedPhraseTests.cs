// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Text;

public class Bip39SeedPhraseTests
{
	private readonly ITestOutputHelper logger;

	public Bip39SeedPhraseTests(ITestOutputHelper logger)
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
		string seedPhrase = Bip39SeedPhrase.Generate(bitLength);
		this.logger.WriteLine(seedPhrase);
		Assert.Equal(expectedWordCount, seedPhrase.Split().Length);
	}

	[Fact]
	public void Generate_Length_ProducesUniquePhrases()
	{
		string seedPhrase = Bip39SeedPhrase.Generate(64);
		string seedPhrase2 = Bip39SeedPhrase.Generate(64);
		Assert.NotEqual(seedPhrase, seedPhrase2);
	}

	[Fact]
	public void Generate_BadLengths()
	{
		Assert.Throws<ArgumentException>(() => Bip39SeedPhrase.Generate(65));
		Assert.Throws<ArgumentException>(() => Bip39SeedPhrase.Generate(16));
		Assert.Throws<ArgumentException>(() => Bip39SeedPhrase.Generate(0));
	}

	[Fact]
	public void GetEntropyLength()
	{
		StringBuilder seedPhraseBuilder = new();
		for (int wordCount = 3; wordCount <= 24; wordCount += 3)
		{
			seedPhraseBuilder.Append("a a  a ");
			int expectedEntropy = wordCount / 3 * 32;
			int actualEntropy = Bip39SeedPhrase.GetEntropyLengthInBits(seedPhraseBuilder.ToString());
			Assert.Equal(expectedEntropy, actualEntropy);
		}
	}

	[Fact]
	public void Generate_Entropy()
	{
		Span<byte> entropy = stackalloc byte[16];
		string seedPhrase = Bip39SeedPhrase.Generate(entropy);
		this.logger.WriteLine(seedPhrase);
		Assert.Equal("abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about", seedPhrase);

		entropy.Fill(0xff);
		seedPhrase = Bip39SeedPhrase.Generate(entropy);
		this.logger.WriteLine(seedPhrase);
		Assert.Equal("zoo zoo zoo zoo zoo zoo zoo zoo zoo zoo zoo wrong", seedPhrase);

		RandomNumberGenerator.Fill(entropy);
		seedPhrase = Bip39SeedPhrase.Generate(entropy);
		this.logger.WriteLine(Convert.ToHexString(entropy));
		this.logger.WriteLine(seedPhrase);
	}

	[Theory]
	[InlineData("property reward account skull verb cruel false labor parent loop donor mutual adult cheese broom that jelly brass vivid later van people cannon join", "AC771406656F28691493E2A0307D0549103C4E4736FF77C367D4BEAF1345885B")]
	[InlineData("funny essay radar tattoo casual dream idle wrestle defy length obtain tobacco", "5E29A6C2EF223A851C2FF239B0026271")]
	public void TryGetEntropy(string seedPhrase, string entropyAsHex)
	{
		Span<byte> entropy = stackalloc byte[Bip39SeedPhrase.GetEntropyLengthInBits(seedPhrase) / 8];
		Assert.True(Bip39SeedPhrase.TryGetEntropy(seedPhrase, entropy, out int bytesWritten, out _, out _));
		Assert.Equal(entropyAsHex, Convert.ToHexString(entropy));
	}
}
