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

		Assert.Equal("utest1qgagna8pk6dwrej80unafkm0zjpwpr75g64vz7g8s0yrrumzaxp38c6jzktsa2nfukpnrv2lvympg8nhqxhg5c0rh58q3wyzd87kl42dlkn60lkfwuw737d7mgs2h46tdwqhd3st8uk53mdr5zruywdw4fazjxa444e2dwdgmg5rud8thyamg7v3amkwc2pe0pccn0n5tlwcjvt5d6c", DefaultAccount.DefaultAddress);
		Assert.Equal("uviewtest1kzusm73duxtt9zgn6pwtmflwp2vxd0pq05vmyt0mvh5n4rcqp7d7dw6yc3n3h0kgp54gv7ze35nu02m36zf7y9p620hhqh04dagje802lajymqykzeltmap3clsjyq0fkwudlqlx7dug73ch3ese4gljcrf4xsv7ju3tlhagkk5jd6m90kq82saeg0tzpydnve4j8uftvamyvuljxza5ne3gerz5cftw4lwr4wq9lg9xcgqc0g4uls57hyhy5dh6wsmclqfcrnzua333jsnj58qj7y6mx5rr8qtzs04ux3u0vw382gtrsmva775s0lqs2246a4gcest2sk7atjdg74hq8vt8e474wky524c99sfp0kmfacmm45ex2257d33jaumdv7f6dv05jxv0f5rqps5u3eyhz24gte8q3rzgy5g4n0kcqy2mv9xk04dvuwsfau2l2wp90us99t2z7yz6hfkp8xmtlappu0ypr7ea3dy6et42mvnw7r8l", DefaultAccount.FullViewing.UnifiedKey);
		Assert.Equal("uivktest1rqjxaa24se33xc5a2hqh3lmlexqvtxp7gk90cx5g4tm8wp79t7vzgkyw6f6ctj7n25n09emmk6z9t88n2689ceu0mq475neujjycmqsq7esavfnvpg7xz37r2rnanal8pnvfhqga6r8efzr863k46p6cjty9qk7e7jjcsmmu0ljdycmy80g3phsh22mx4sts5wy0eaerg87fa5l8lrdlxj5rmn0ueacfun88qthuxfx7d9jzq4fm05hlmz775h9rc8eeuvswkr5q3p2zyfx0anz4juu89e7hvc3362jp2lm0ktnz0vkzpefmruyf5l2rz4vd95pqj0twaxrw6ld26asshu63ck", DefaultAccount.IncomingViewing.UnifiedKey);
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

	[Fact]
	public void HasDiversifiableKeys()
	{
		// Default account has diversifiable keys.
		Assert.True(DefaultAccount.HasDiversifiableKeys);

		// Orchard only accounts have diversifiable keys.
		Assert.True(new ZcashAccount(UnifiedViewingKey.Incoming.Create(DefaultAccount.IncomingViewing.Orchard!)).HasDiversifiableKeys);

		// Sapling only accounts have diversifiable keys.
		Assert.True(new ZcashAccount(UnifiedViewingKey.Incoming.Create(DefaultAccount.IncomingViewing.Sapling!)).HasDiversifiableKeys);

		// Transparent only accounts have no diversifiable keys.
		Assert.False(new ZcashAccount(UnifiedViewingKey.Incoming.Create(DefaultAccount.IncomingViewing.Transparent!)).HasDiversifiableKeys);
	}
}
