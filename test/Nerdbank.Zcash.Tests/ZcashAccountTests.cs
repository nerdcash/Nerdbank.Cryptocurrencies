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

		Assert.Equal("utest10ps8s9pl435yhqn8v20mwraslc3jfz26c8vh62d5xnh0hlrmpqgtj8v5ffdm48nmv56pnjhnrwwnvsj2vl6fmyytmzwdynupsgpl7jqd4nvet4l8ayddnjgy6lruagderh5wxr0dgl259m63h2gf2canlfm88q69huf9uwr5czypdkhlh3n2heucy6qfvysna0r9usamd6y0y9an3nu", DefaultAccount.DefaultAddress);
		Assert.Equal("uviewtest1hcsgl8c0cvj0t7awn2jvfhqllhn7nxnczqsdwnuycxcky526hsy785at7pm4ukmhee2fu88729tcqrfju8z9tsuvsa82ahr9eypj23er63h8tfgu3wxj8l60dhhzwrhrgph8gxwa37a5spvhxt7elgv0da6hctp82ffrcx3vavg703huwmm6j7722pz20pkttu3pqu3pmcq7wd5dtg9lphd7zd358kpdn4hd63gscqzqml7z35x4fa5de5gf0yzanpxm6fm208zj3ge2x9jtry0849rs5t2zsnvwee000ttnr3j4vf5905lqt654xgpv90a9sfl04zxa7f2cy3q5md9pnz0vfyme8x2awj2j9xshuaaaalxty3ql2kjt5sa52rqftj2upf5sdu5vd5qy69kevkcv8cdwwkw2khqkkgv9u7gwhr0j98zkpcfwa4w45ggltw39wutph22x6zmq5ygn2staqqcp8kxtcz2mtjc99wf45qdrewwq", DefaultAccount.FullViewing.UnifiedKey);
		Assert.Equal("uivktest1p6jzxrcdv4eg44l8whmzzjp2e6t253spt4zq3c0kh7up5p5z30m6hr9ps3srgfu26ayp9jtrwa296j3fvc286c3twqwpqqj0amdvyyj02crmsg9uw9vmrazhnhuwlnmyh4nrd0mt0hzmfakutaguwyuzyd7lf4efayz6d5e4rvsy3jzm899uqsa2w546mala8m97rcdx29qm4hecenl8fkrgxuade3492qwx752sv3cd3xd3a2tptmear5tlnjfagk9gyzrr6384d5wpaxellh8ghdu6klnaphz58g0r9r2esskcvgv8xcdq4l4q9urjtp96vxhlxsqq2ufzpp3ywlldzzn0f7", DefaultAccount.IncomingViewing.UnifiedKey);
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

	[Fact]
	public void FullViewingAccount()
	{
		ZcashAccount fullViewAccount = new(DefaultAccount.FullViewing!.UnifiedKey);

		Assert.Null(fullViewAccount.Spending);
		Assert.NotNull(fullViewAccount.FullViewing?.Transparent);
		Assert.NotNull(fullViewAccount.FullViewing?.Sapling);
		Assert.NotNull(fullViewAccount.FullViewing?.Orchard);
		Assert.Equal(DefaultAccount.FullViewing!.UnifiedKey, fullViewAccount.FullViewing?.UnifiedKey);
	}

	[Fact]
	public void IncomingViewingAccount()
	{
		ZcashAccount incomingViewAccount = new(DefaultAccount.IncomingViewing.UnifiedKey);

		Assert.Null(incomingViewAccount.Spending);
		Assert.Null(incomingViewAccount.FullViewing);
		Assert.NotNull(incomingViewAccount.IncomingViewing?.Transparent);
		Assert.NotNull(incomingViewAccount.IncomingViewing?.Sapling);
		Assert.NotNull(incomingViewAccount.IncomingViewing?.Orchard);
		Assert.Equal(DefaultAccount.IncomingViewing!.UnifiedKey, incomingViewAccount.IncomingViewing?.UnifiedKey);
	}
}
