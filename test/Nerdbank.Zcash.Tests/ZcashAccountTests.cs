// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public class ZcashAccountTests : TestBase
{
	private static readonly Zip32HDWallet Zip32 = new(Mnemonic, ZcashNetwork.TestNet);
	private readonly ITestOutputHelper logger;

	public ZcashAccountTests(ITestOutputHelper logger)
	{
		this.logger = logger;
	}

	protected static ZcashAccount DefaultAccount { get; } = new(Zip32, 0);

	protected static ZcashAccount AlternateAccount { get; } = new(Zip32, 1);

	[Fact]
	public void DefaultAccountProperties()
	{
		Assert.Equal(0u, DefaultAccount.Index);
		Assert.NotNull(DefaultAccount.Spending?.Orchard);
		Assert.NotNull(DefaultAccount.Spending?.Sapling);
		Assert.NotNull(DefaultAccount.Spending?.Transparent);
		Assert.NotNull(DefaultAccount.FullViewing?.Orchard);
		Assert.NotNull(DefaultAccount.FullViewing?.Sapling);
		Assert.NotNull(DefaultAccount.FullViewing?.Transparent);
		Assert.NotNull(DefaultAccount.IncomingViewing?.Orchard);
		Assert.NotNull(DefaultAccount.IncomingViewing?.Sapling);
		Assert.NotNull(DefaultAccount.IncomingViewing?.Transparent);
		Assert.NotNull(DefaultAccount.DefaultAddress);

		this.logger.WriteLine(DefaultAccount.DefaultAddress);
		this.logger.WriteLine(DefaultAccount.FullViewing.UnifiedKey);
		this.logger.WriteLine(DefaultAccount.IncomingViewing.UnifiedKey);

		Assert.Equal("utest1keymmgtleqfv27sd9n2aasp2c9nz6ulm0s2mhxv7ur2rwskts5q4259330t9npjj7uufyf4pcgx3xamxngerke5masc23j62gqsehm205ep9vhxvfmf2jufstryxz2nve929kksjcq2kmecmu3f5pck25mp6wu3pyumzwfplwdlren36s4544555ca935x72y2ha2mmtmgdkycwfycw", DefaultAccount.DefaultAddress);
		Assert.Equal("uviewtest1ny79zf6w3xzxdu99vwmf6lz3xhk0kcuwr6a47tnnngs8hx73h4rwnhvqq8e3c8kzcs4hveu59zt93usacud4eg3zk5daq9xumsf0g5ylsy4kfxtwdachjmscunrr2f4cefwhgvrmtn6u2wjf9s3kr6mrqajvt5m9svernq30fp2zfv9d0kupp58dk6tqaael8uyjex0k390fpunqt7uhts9064jrn04e0vw6vqjfh3af4udfypd9jecz4z9nff72efguz3x06cm6sj2l6lxttw4wd4uv3ekcff7v63q7t4k9tjep8ypkcy7xwl9vt6tmgxk0jqzr94krpytg4372ljc82t0804pszcah9cepnldzwnkktvewt06n5ngprnkchl2wa83pztj75775zvmy0hlalgqapmdf9um753xhmpgrq0gz47pmpxkzh605gc6qkd6lx20lvtwletq3f4wlf57672xflpjhqwp25f9gtrd4qddghgz60ucc", DefaultAccount.FullViewing.UnifiedKey);
		Assert.Equal("uivktest1t7lp5zp6xdg5d8x6ela0yfumn0883wdkderjd4t8ykkjje58n9pc3r5te5hddece2drz6suehs76l6d62cm262ngteumm9z5w3qekf4gsp4p662cetxrg6hhpwwcrh4gx3kv6m453p88qvugfrh8uk6m43mq6q8nza2lelafumvdwqpjlglyzx6qkrfje6pl9dnpst732ye46kvg43nz4kv6x4h0j9pgwk6jf4tprc48eut8h2grn4kmf6rnf7jky7mlagjdms9q6zdrwne9zg3fwzp5a6j3fh7fcew75lgmv7hfupwytxnm3e7zgwt34yxdwc8cdd0tan6w0vf72j4qrfgnnn", DefaultAccount.IncomingViewing.UnifiedKey);
	}

	[Fact]
	public void GetDiversifiedAddress_TimeBased()
	{
		UnifiedAddress diversified = DefaultAccount.GetDiversifiedAddress();
		this.logger.WriteLine($"Default:     {DefaultAccount.DefaultAddress}");
		this.logger.WriteLine($"Diversified: {diversified}");
		Assert.NotEqual(DefaultAccount.DefaultAddress, diversified);
	}

	[Fact]
	public void GetDiversifiedAddress_ManualIndex()
	{
		DiversifierIndex index = default;
		UnifiedAddress diversified = DefaultAccount.GetDiversifiedAddress(ref index);

		// We happen to know that this particular wallet's sapling key doesn't produce a valid diversifier at index 0.
		Assert.NotEqual(default, index);
		this.logger.WriteLine($"Diversifier index: {index.ToBigInteger()}");

		// Use it again and verify we get the same answer.
		DiversifierIndex index2 = index;
		UnifiedAddress diversified2 = DefaultAccount.GetDiversifiedAddress(ref index2);

		Assert.Equal(index, index2);
		Assert.Equal(diversified, diversified2);
	}

	[Fact]
	public void AddressSendsToThisAccount_Unified()
	{
		Assert.True(DefaultAccount.AddressSendsToThisAcount(DefaultAccount.DefaultAddress));
		Assert.True(DefaultAccount.AddressSendsToThisAcount(DefaultAccount.GetDiversifiedAddress()));
		Assert.False(DefaultAccount.AddressSendsToThisAcount(AlternateAccount.DefaultAddress));
	}

	[Fact]
	public void AddressSendsToThisAccount_Sapling()
	{
		Assert.True(DefaultAccount.AddressSendsToThisAcount(DefaultAccount.IncomingViewing.Sapling!.DefaultAddress));
		Assert.False(DefaultAccount.AddressSendsToThisAcount(AlternateAccount.IncomingViewing.Sapling!.DefaultAddress));
	}

	/// <summary>
	/// Verifies that a compound unified address with receivers both inside and outside the account
	/// is recognized as an unfriendly address.
	/// </summary>
	[Fact]
	public void AddressSendsToThisAccount_HijackerDefense()
	{
		// Craft a UA that has a receiver in this account and outside this account.
		// An attacker might craft such an address to fool the owner of an account into believing
		// that this is a safe address to use and share, when in fact depending on which pool receiver is used,
		// ZEC sent to it might in fact go to the attacker instead of the victim.
		ZcashAddress unfriendly = UnifiedAddress.Create(
			DefaultAccount.IncomingViewing.Orchard!.DefaultAddress,
			AlternateAccount.IncomingViewing.Sapling!.DefaultAddress);
		Assert.False(DefaultAccount.AddressSendsToThisAcount(unfriendly));
	}
}
