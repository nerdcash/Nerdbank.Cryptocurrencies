// Copyright (c) Andrew Arnott. All rights reserved.
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
	public void Encoding_Test()
	{
		SpendingKey key = new Zip32HDWallet(Mnemonic, ZcashNetwork.TestNet).CreateOrchardAccount().SpendingKey;
		this.logger.WriteLine(key.Encoded);
		Assert.Equal("secret-orchard-sk-test1te7l50dm25afq89clh8r2ft34lufgxfyn0lwe6hxvvhyzfp3xfeqz6u5r0", key.Encoded);
	}

	[Fact]
	public void Encoding_Main()
	{
		SpendingKey key = new Zip32HDWallet(Mnemonic, ZcashNetwork.MainNet).CreateOrchardAccount().SpendingKey;
		this.logger.WriteLine(key.Encoded);
		Assert.Equal("secret-orchard-sk-main1te7l50dm25afq89clh8r2ft34lufgxfyn0lwe6hxvvhyzfp3xfeqctpsk7", key.Encoded);
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
