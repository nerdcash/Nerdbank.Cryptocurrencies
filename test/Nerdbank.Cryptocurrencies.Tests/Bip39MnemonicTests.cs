// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

public class Bip39MnemonicTests
{
	private const string SeedPhrase = "funny essay radar tattoo casual dream idle wrestle defy length obtain tobacco";
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
		string seedPhrase = Bip39Mnemonic.Create(bitLength).SeedPhrase;
		this.logger.WriteLine(seedPhrase);
		Assert.Equal(expectedWordCount, seedPhrase.Split().Length);
	}

	[Fact]
	public void Generate_Length_ProducesUniquePhrases()
	{
		string seedPhrase = Bip39Mnemonic.Create(64).SeedPhrase;
		string seedPhrase2 = Bip39Mnemonic.Create(64).SeedPhrase;
		Assert.NotEqual(seedPhrase, seedPhrase2);
	}

	[Fact]
	public void Generate_BadLengths()
	{
		Assert.Throws<ArgumentException>(() => Bip39Mnemonic.Create(65));
		Assert.Throws<ArgumentException>(() => Bip39Mnemonic.Create(16));
		Assert.Throws<ArgumentException>(() => Bip39Mnemonic.Create(0));
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
		Assert.True(Bip39Mnemonic.TryParse(SeedPhrase.ToUpperInvariant(), out Bip39Mnemonic? mnemonic, out _, out _));
		Assert.Equal("5E29A6C2EF223A851C2FF239B0026271", Convert.ToHexString(mnemonic.Entropy));
		Assert.Equal(SeedPhrase, mnemonic.SeedPhrase);
	}

	[Fact]
	public void TryParse_ToleratesExtraWhitespace()
	{
		const string CleanSeedPhrase = "funny essay radar tattoo casual dream idle wrestle defy length obtain tobacco";
		const string MessySeedPhrase = "  funny	essay   radar tattoo casual dream idle wrestle defy \n length obtain tobacco \n";
		Assert.True(Bip39Mnemonic.TryParse(MessySeedPhrase.ToUpperInvariant(), out Bip39Mnemonic? mnemonic, out _, out _));
		Assert.Equal("5E29A6C2EF223A851C2FF239B0026271", Convert.ToHexString(mnemonic.Entropy));
		Assert.Equal(CleanSeedPhrase, mnemonic.SeedPhrase);
	}

	[Fact]
	public void TryParse_WithExplicitPassword()
	{
		const string Password = "some password";
		Assert.True(Bip39Mnemonic.TryParse(SeedPhrase, Password.AsMemory(), out Bip39Mnemonic? mnemonic, out _, out _));
		Assert.Equal("5E29A6C2EF223A851C2FF239B0026271", Convert.ToHexString(mnemonic.Entropy));
		Assert.Equal(Password.AsMemory(), mnemonic.Password);
		Assert.Equal(SeedPhrase, mnemonic.SeedPhrase);
	}

	[Fact]
	public void TryParse_WithImplicitPassword()
	{
		const string Password = "somepassword";
		Assert.True(Bip39Mnemonic.TryParse($"{SeedPhrase} {Password}", out Bip39Mnemonic? mnemonic, out _, out _));
		Assert.Equal("5E29A6C2EF223A851C2FF239B0026271", Convert.ToHexString(mnemonic.Entropy));
		Assert.Equal(Password.AsMemory().ToString(), mnemonic.Password.ToString());
		Assert.Equal(SeedPhrase, mnemonic.SeedPhrase);
	}

	[Fact]
	public void TryParse_WithTwoExtraWords()
	{
		const string SeedPhrase = "funny essay radar tattoo casual dream idle wrestle defy length obtain tobacco obtain tobacco";
		Assert.False(Bip39Mnemonic.TryParse(SeedPhrase, out Bip39Mnemonic? mnemonic, out DecodeError? decodeError, out string? errorMessage));
		this.logger.WriteLine(errorMessage);
		Assert.Equal(DecodeError.BadWordCount, decodeError);
	}

	[Fact]
	public void TryParse_EmptyString_WithPasswordParameter()
	{
		Assert.False(Bip39Mnemonic.TryParse(string.Empty, string.Empty, out Bip39Mnemonic? mnemonic, out DecodeError? decodeError, out string? errorMessage));
		Assert.Null(mnemonic);
		Assert.Equal(DecodeError.BadWordCount, decodeError);
		this.logger.WriteLine(errorMessage);
	}

	[Fact]
	public void TryParse_EmptyString()
	{
		Assert.False(Bip39Mnemonic.TryParse(string.Empty, out Bip39Mnemonic? mnemonic, out DecodeError? decodeError, out string? errorMessage));
		Assert.Null(mnemonic);
		Assert.Equal(DecodeError.BadWordCount, decodeError);
		this.logger.WriteLine(errorMessage);
	}

	[Fact]
	public void TryParse_OnlyWhitespace()
	{
		Assert.False(Bip39Mnemonic.TryParse("  ", out Bip39Mnemonic? mnemonic, out DecodeError? decodeError, out string? errorMessage));
		Assert.Null(mnemonic);
		Assert.Equal(DecodeError.BadWordCount, decodeError);
		this.logger.WriteLine(errorMessage);
	}

	[Fact]
	public void TryParse_IncompletelyTyped_WithPasswordParameter()
	{
		Assert.False(Bip39Mnemonic.TryParse("f", string.Empty, out Bip39Mnemonic? mnemonic, out DecodeError? decodeError, out string? errorMessage));
		Assert.Null(mnemonic);
		Assert.Equal(DecodeError.BadWordCount, decodeError);
		this.logger.WriteLine(errorMessage);
	}

	[Fact]
	public void TryParse_IncompletelyTyped()
	{
		Assert.False(Bip39Mnemonic.TryParse("f", out Bip39Mnemonic? mnemonic, out DecodeError? decodeError, out string? errorMessage));
		Assert.Null(mnemonic);
		Assert.Equal(DecodeError.BadWordCount, decodeError);
		this.logger.WriteLine(errorMessage);
	}

	[Fact]
	public void Parse_PasswordWhitespaceSignificant()
	{
		Bip39Mnemonic oneSpace = Bip39Mnemonic.Parse(SeedPhrase, " ");
		Bip39Mnemonic twoSpaces = Bip39Mnemonic.Parse(SeedPhrase, "  ");
		Assert.False(oneSpace.Seed.SequenceEqual(twoSpaces.Seed));
	}

	[Fact]
	public void Parse()
	{
		Assert.Equal(0, Bip39Mnemonic.Parse("diary slender airport").Password.Length);
		Assert.Equal("password", Bip39Mnemonic.Parse("diary slender airport", "password".AsMemory()).Password.ToString());

		Assert.Throws<FormatException>(() => Bip39Mnemonic.Parse("wrong words"));
		Assert.Throws<FormatException>(() => Bip39Mnemonic.Parse("wrong words", "password".AsMemory()));
	}

	[Theory]
	[InlineData("property reward account skull verb cruel false labor parent loop donor mutual adult cheese broom that jelly brass vivid later van people cannon join", "AC771406656F28691493E2A0307D0549103C4E4736FF77C367D4BEAF1345885B", "0fae82d3cd28dc768634a48c29c4cc22aa6981553f0056774234d85fa7955d0a6c5f67b768e3ebbf12f152e108db9720c46cebdc5969b0ccf7a92b721536cacd")]
	[InlineData("funny essay radar tattoo casual dream idle wrestle defy length obtain tobacco", "5E29A6C2EF223A851C2FF239B0026271", "12a5497088826d8ba3a1320606507fdc551720936d46e2afa213148f6269422dace2c5218611e1acde2d7f392977f33393fa9181865ae5c7d756b28597a63d7a")]
	public void TryParse(string seedPhrase, string entropyAsHex, string seedAsHex)
	{
		Assert.True(Bip39Mnemonic.TryParse(seedPhrase, out Bip39Mnemonic? mnemonic, out _, out _));
		Assert.Equal(entropyAsHex, Convert.ToHexString(mnemonic.Entropy));
		Assert.Equal(seedAsHex, Convert.ToHexString(mnemonic.Seed), ignoreCase: true);
	}

	[Fact]
	public void ToString_Is_Seedphrase()
	{
		Bip39Mnemonic mnemonic = Bip39Mnemonic.Create(64);
		Assert.Equal(mnemonic.SeedPhrase, mnemonic.ToString());
	}
}
