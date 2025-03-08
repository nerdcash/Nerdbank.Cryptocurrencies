// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Zcash.Orchard;

namespace Orchard;

public class SpendingKeyTests : TestBase
{
	private readonly ITestOutputHelper logger;

	public SpendingKeyTests(ITestOutputHelper logger)
	{
		this.logger = logger;
	}

	[Fact]
	public void TextEncoding_Test()
	{
		SpendingKey key = new Zip32HDWallet(Mnemonic, ZcashNetwork.TestNet).CreateOrchardAccount().SpendingKey;
		this.logger.WriteLine(key.TextEncoding);
		Assert.Equal("secret-orchard-sk-test16swpunh96ywktramm4ft9st3872yhxujn3j746aq9vckt7ws94esuqcees", key.TextEncoding);
	}

	[Fact]
	public void TextEncoding_Main()
	{
		SpendingKey key = new Zip32HDWallet(Mnemonic, ZcashNetwork.MainNet).CreateOrchardAccount().SpendingKey;
		this.logger.WriteLine(key.TextEncoding);
		Assert.Equal("secret-orchard-sk-main17nmlypwfae54wumq4l2mh2c5yphf6n93k53ds8xuryg04xszj53sgf4cer", key.TextEncoding);
	}

	[Theory, PairwiseData]
	public void Network(ZcashNetwork network)
	{
		SpendingKey key = new Zip32HDWallet(Mnemonic, network).CreateOrchardAccount().SpendingKey;
		Assert.Equal(network, key.Network);
	}

	[Fact]
	public void FullViewingKey()
	{
		SpendingKey key = new Zip32HDWallet(Mnemonic, ZcashNetwork.TestNet).CreateOrchardAccount().SpendingKey;
		Assert.NotNull(key.FullViewingKey);
		Assert.Equal(ZcashNetwork.TestNet, key.Network);
	}

	[Fact]
	public void IncomingViewingKey()
	{
		SpendingKey key = new Zip32HDWallet(Mnemonic, ZcashNetwork.TestNet).CreateOrchardAccount().SpendingKey;
		Assert.NotNull(key.IncomingViewingKey);
		Assert.Equal(ZcashNetwork.TestNet, key.Network);
	}
}
