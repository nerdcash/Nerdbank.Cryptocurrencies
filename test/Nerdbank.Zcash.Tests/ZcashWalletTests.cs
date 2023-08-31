// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using NuGet.Frameworks;

public class ZcashWalletTests : TestBase
{
	private readonly ZcashWallet wallet = new ZcashWallet(Mnemonic, ZcashNetwork.TestNet);
	private readonly ITestOutputHelper logger;

	public ZcashWalletTests(ITestOutputHelper logger)
	{
		this.logger = logger;
	}

	protected ZcashWallet.Account DefaultAccount => this.wallet.Accounts[0];

	[Fact]
	public void Ctor_Mnemonic_HasOneAccount()
	{
		Assert.Single(new ZcashWallet(Mnemonic, ZcashNetwork.TestNet).Accounts);
	}

	[Fact]
	public void Ctor_Seed_HasOneAccount()
	{
		Assert.Single(new ZcashWallet(Mnemonic.Seed, ZcashNetwork.TestNet).Accounts);
	}

	[Fact]
	public void Network_MatcherCtor()
	{
		Assert.Equal(ZcashNetwork.TestNet, this.wallet.Network);
	}

	[Fact]
	public void Mnemonic_MatchesCtor()
	{
		Assert.Equal(Mnemonic, this.wallet.Mnemonic);
	}

	[Fact]
	public void Mnemonic_UsingSeedCtor()
	{
		Assert.Null(new ZcashWallet(Mnemonic.Seed, ZcashNetwork.TestNet).Mnemonic);
	}

	[Fact]
	public void Seed_MatchesCtor()
	{
		Assert.True(Mnemonic.Seed.SequenceEqual(this.wallet.Seed.Span));
	}

	[Fact]
	public void DefaultAccountProperties()
	{
		Assert.Equal(0u, this.DefaultAccount.Index);
		Assert.NotNull(this.DefaultAccount.Spending?.Orchard);
		Assert.NotNull(this.DefaultAccount.Spending?.Sapling);
		Assert.NotNull(this.DefaultAccount.Spending?.Transparent);
		Assert.NotNull(this.DefaultAccount.FullViewing?.Orchard);
		Assert.NotNull(this.DefaultAccount.FullViewing?.Sapling);
		Assert.NotNull(this.DefaultAccount.FullViewing?.Transparent);
		Assert.NotNull(this.DefaultAccount.IncomingViewing?.Orchard);
		Assert.NotNull(this.DefaultAccount.IncomingViewing?.Sapling);
		Assert.NotNull(this.DefaultAccount.IncomingViewing?.Transparent);
		Assert.NotNull(this.DefaultAccount.DefaultAddress);

		this.logger.WriteLine(this.DefaultAccount.DefaultAddress);
		this.logger.WriteLine(this.DefaultAccount.FullViewing.UnifiedKey);
		this.logger.WriteLine(this.DefaultAccount.IncomingViewing.UnifiedKey);

		Assert.Equal("utest1keymmgtleqfv27sd9n2aasp2c9nz6ulm0s2mhxv7ur2rwskts5q4259330t9npjj7uufyf4pcgx3xamxngerke5masc23j62gqsehm205ep9vhxvfmf2jufstryxz2nve929kksjcq2kmecmu3f5pck25mp6wu3pyumzwfplwdlren36s4544555ca935x72y2ha2mmtmgdkycwfycw", this.DefaultAccount.DefaultAddress);
		Assert.Equal("uviewtest1ny79zf6w3xzxdu99vwmf6lz3xhk0kcuwr6a47tnnngs8hx73h4rwnhvqq8e3c8kzcs4hveu59zt93usacud4eg3zk5daq9xumsf0g5ylsy4kfxtwdachjmscunrr2f4cefwhgvrmtn6u2wjf9s3kr6mrqajvt5m9svernq30fp2zfv9d0kupp58dk6tqaael8uyjex0k390fpunqt7uhts9064jrn04e0vw6vqjfh3af4udfypd9jecz4z9nff72efguz3x06cm6sj2l6lxttw4wd4uv3ekcff7v63q7t4k9tjep8ypkcy7xwl9vt6tmgxk0jqzr94krpytg4372ljc82t0804pszcah9cepnldzwnkktvewt06n5ngprnkchl2wa83pztj75775zvmy0hlalgqapmdf9um753xhmpgrq0gz47pmpxkzh605gc6qkd6lx20lvtwletq3f4wlf57672xflpjhqwp25f9gtrd4qddghgz60ucc", this.DefaultAccount.FullViewing.UnifiedKey);
		Assert.Equal("uivktest1t7lp5zp6xdg5d8x6ela0yfumn0883wdkderjd4t8ykkjje58n9pc3r5te5hddece2drz6suehs76l6d62cm262ngteumm9z5w3qekf4gsp4p662cetxrg6hhpwwcrh4gx3kv6m453p88qvugfrh8uk6m43mq6q8nza2lelafumvdwqpjlglyzx6qkrfje6pl9dnpst732ye46kvg43nz4kv6x4h0j9pgwk6jf4tprc48eut8h2grn4kmf6rnf7jky7mlagjdms9q6zdrwne9zg3fwzp5a6j3fh7fcew75lgmv7hfupwytxnm3e7zgwt34yxdwc8cdd0tan6w0vf72j4qrfgnnn", this.DefaultAccount.IncomingViewing.UnifiedKey);
	}

	[Fact]
	public void GetDiversifiedAddress_TimeBased()
	{
		UnifiedAddress diversified = this.DefaultAccount.GetDiversifiedAddress();
		this.logger.WriteLine($"Default:     {this.DefaultAccount.DefaultAddress}");
		this.logger.WriteLine($"Diversified: {diversified}");
		Assert.NotEqual(this.DefaultAccount.DefaultAddress, diversified);
	}

	[Fact]
	public void GetDiversifiedAddress_ManualIndex()
	{
		DiversifierIndex index = default;
		UnifiedAddress diversified = this.DefaultAccount.GetDiversifiedAddress(ref index);

		// We happen to know that this particular wallet's sapling key doesn't produce a valid diversifier at index 0.
		Assert.NotEqual(default, index);
		this.logger.WriteLine($"Diversifier index: {index.ToBigInteger()}");

		// Use it again and verify we get the same answer.
		DiversifierIndex index2 = index;
		UnifiedAddress diversified2 = this.DefaultAccount.GetDiversifiedAddress(ref index2);

		Assert.Equal(index, index2);
		Assert.Equal(diversified, diversified2);
	}

	[Fact]
	public void AddressSendsToThisAccount_Unified()
	{
		ZcashWallet.Account otherAccount = this.wallet.AddAccount();

		Assert.True(this.DefaultAccount.AddressSendsToThisAcount(this.DefaultAccount.DefaultAddress));
		Assert.True(this.DefaultAccount.AddressSendsToThisAcount(this.DefaultAccount.GetDiversifiedAddress()));
		Assert.False(this.DefaultAccount.AddressSendsToThisAcount(otherAccount.DefaultAddress));
	}

	[Fact]
	public void AddressSendsToThisAccount_Sapling()
	{
		Assert.True(this.DefaultAccount.AddressSendsToThisAcount(this.DefaultAccount.IncomingViewing.Sapling!.DefaultAddress));
		Assert.False(this.DefaultAccount.AddressSendsToThisAcount(this.wallet.AddAccount().IncomingViewing.Sapling!.DefaultAddress));
	}

	/// <summary>
	/// Verifies that a compound unified address with receivers both inside and outside the account
	/// is recognized as an unfriendly address.
	/// </summary>
	[Fact]
	public void AddressSendsToThisAccount_HijackerDefense()
	{
		ZcashWallet.Account otherAccount = this.wallet.AddAccount();

		// Craft a UA that has a receiver in this account and outside this account.
		// An attacker might craft such an address to fool the owner of an account into believing
		// that this is a safe address to use and share, when in fact depending on which pool receiver is used,
		// ZEC sent to it might in fact go to the attacker instead of the victim.
		ZcashAddress unfriendly = UnifiedAddress.Create(this.DefaultAccount.IncomingViewing.Orchard!.DefaultAddress, otherAccount.IncomingViewing.Sapling!.DefaultAddress);
		Assert.False(this.DefaultAccount.AddressSendsToThisAcount(unfriendly));
	}
}
