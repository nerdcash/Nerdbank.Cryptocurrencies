// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using OrchardFVK = Nerdbank.Zcash.Orchard.FullViewingKey;
using OrchardSK = Nerdbank.Zcash.Zip32HDWallet.Orchard.ExtendedSpendingKey;
using SaplingFVK = Nerdbank.Zcash.Sapling.DiversifiableFullViewingKey;
using SaplingSK = Nerdbank.Zcash.Zip32HDWallet.Sapling.ExtendedSpendingKey;

public class UnifiedViewingKeyTests : TestBase
{
	private readonly ITestOutputHelper logger;

	public UnifiedViewingKeyTests(ITestOutputHelper logger)
	{
		this.logger = logger;
	}

	[Fact]
	public void Create_NonUniqueTypes()
	{
		Zip32HDWallet wallet = new(Mnemonic, ZcashNetwork.TestNet);
		OrchardSK account1 = wallet.CreateOrchardAccount(0);
		OrchardSK account2 = wallet.CreateOrchardAccount(1);

		ArgumentException ex = Assert.Throws<ArgumentException>(
			() => UnifiedViewingKey.Create(new[] { account1.FullViewingKey, account2.FullViewingKey }));
		this.logger.WriteLine(ex.Message);
	}

	[Fact]
	public void Create_MixedNetworks()
	{
		Zip32HDWallet testWallet = new(Mnemonic, ZcashNetwork.TestNet);
		Zip32HDWallet mainWallet = new(Mnemonic, ZcashNetwork.MainNet);
		OrchardSK orchard = testWallet.CreateOrchardAccount(0);
		SaplingSK sapling = mainWallet.CreateSaplingAccount(0);
		ArgumentException ex = Assert.Throws<ArgumentException>(
			() => UnifiedViewingKey.Create(orchard.FullViewingKey, sapling.FullViewingKey));
		this.logger.WriteLine(ex.Message);
	}

	[Fact]
	public void Create_MixedViewingTypes()
	{
		Zip32HDWallet wallet = new(Mnemonic, ZcashNetwork.TestNet);
		OrchardSK orchard = wallet.CreateOrchardAccount(0);
		SaplingSK sapling = wallet.CreateSaplingAccount(0);
		ArgumentException ex = Assert.Throws<ArgumentException>(
			() => UnifiedViewingKey.Create(orchard.FullViewingKey, sapling.IncomingViewingKey));
		this.logger.WriteLine(ex.Message);
	}

	[Fact]
	public void ToString_ReturnsEncoding()
	{
		Zip32HDWallet wallet = new(Mnemonic, ZcashNetwork.MainNet);
		OrchardSK orchard = wallet.CreateOrchardAccount(0);
		UnifiedViewingKey uvk = UnifiedViewingKey.Create(orchard.FullViewingKey);
		Assert.Equal(uvk.ViewingKey, uvk.ToString());
	}

	[Fact]
	public void ImplicitCastToString_ReturnsEncoding()
	{
		Zip32HDWallet wallet = new(Mnemonic, ZcashNetwork.MainNet);
		OrchardSK orchard = wallet.CreateOrchardAccount(0);
		UnifiedViewingKey uvk = UnifiedViewingKey.Create(orchard.FullViewingKey);
		string uvkString = uvk;
		Assert.Equal(uvk.ViewingKey, uvkString);
	}

	[Fact]
	public void Create_Orchard_FVK()
	{
		Zip32HDWallet wallet = new(Mnemonic, ZcashNetwork.MainNet);
		OrchardSK orchard = wallet.CreateOrchardAccount(0);
		UnifiedViewingKey.Full uvk = UnifiedViewingKey.Create(orchard.FullViewingKey);

		Assert.Equal(
			"uview12z0pgg2u7q5ky5wzas8mmgcs4zy8cmdyt62tn3ecpdurnqqnlldx6j73600qe4xkz7jp4w37elr2d48jm5ktuvlm5x8z5ke6cg3x8m6sk5sruh4xnjk93h86fls0uyhhtaj8vu0mw0t7cr74vc8ra2360yhamnskk7a7ahsasndmagsmuhs27lqdyjsz9",
			uvk.ViewingKey);
	}

	[Fact]
	public void Create_Orchard_IVK()
	{
		Zip32HDWallet wallet = new(Mnemonic, ZcashNetwork.MainNet);
		OrchardSK orchard = wallet.CreateOrchardAccount(0);
		UnifiedViewingKey.Incoming uvk = Assert.IsType<UnifiedViewingKey.Incoming>(UnifiedViewingKey.Create(orchard.IncomingViewingKey));

		Assert.Equal(
			"uivk1zz6k7d37rldq7mk0n4uegyvesggreucz8h48nnrvlzznhvv3kqm4zp7wngksclaptu8hfqc6l57takh5x6jypygqp5m7d36gmxlxakgzqkp3luqrdgnk97k556wezjydjkmqkvkvf6",
			uvk.ViewingKey);
	}

	[Fact]
	public void Create_Sapling_Full()
	{
		Zip32HDWallet wallet = new(Mnemonic, ZcashNetwork.MainNet);
		SaplingSK sapling = wallet.CreateSaplingAccount(0);
		UnifiedViewingKey.Full uvk = UnifiedViewingKey.Create(sapling.FullViewingKey);

		Assert.Equal(
			"uview17x4ug5kp5shmrywnsadcce6dmyj54ey9hgu4yek25zfw5vnhrghectdsdgsgc099jj99amrxkl5afvyv50jxpwg3u53rq4hzhtln8gurtu5vey602wp0q6xcyxnjtat0a5hn4z82am7fygd42u4h3n27ndtpkx9w6p8nv20k7f5swak7g4wvhte39sasxm9rxea3y7q3c537csxlut9jaf8g3j3ddl02x4j0j7zg0qglvx55",
			uvk.ViewingKey);
	}

	[Fact]
	public void Create_Sapling_IVK()
	{
		Zip32HDWallet wallet = new(Mnemonic, ZcashNetwork.MainNet);
		SaplingSK sapling = wallet.CreateSaplingAccount(0);
		UnifiedViewingKey.Incoming uvk = Assert.IsType<UnifiedViewingKey.Incoming>(UnifiedViewingKey.Create(sapling.IncomingViewingKey));

		Assert.Equal(
			"uivk1fahcymtzgy7kza5nvvq9s0wjxsgjhwt5aunczjeg2sszfrutf0gdrfue9en54ecmlzfxr7hsfwyaqzygzf8f35ng6za3jzxfceh2t093asl04t299wca03ezeuchn4h2cf5s66zdzr",
			uvk.ViewingKey);
	}

	[Fact]
	public void Create_Orchard_Sapling()
	{
		Zip32HDWallet wallet = new(Mnemonic, ZcashNetwork.MainNet);
		OrchardSK orchard = wallet.CreateOrchardAccount(0);
		SaplingSK sapling = wallet.CreateSaplingAccount(0);
		UnifiedViewingKey.Full uvk = UnifiedViewingKey.Create(orchard.FullViewingKey, sapling.FullViewingKey);

		// This expected value came from YWallet when generated with the test seed phrase.
		Assert.Equal(
			"uview1tz7evwpdc274ekw8a7pej527wpxmchsv0hj7g65fhjgpsvzjzc3qhe79qea74c7repnc6mya6wdkawl6chk0vrx4u9dxfwhd9kl9l8k48qvy7tjtuxc4wzc0ety3t0r4p9mz88w2736m4l9r7d7t8hhj92wdxcgaukqkxmnchpn45zn5pwdmd99q6msfv7dglgqpkq95rgglsmklr7quc27xhy03fs2nha4xuufzns3glh4560tccrm739pqh6sfs33m8d50gyv5jshyra9uwktf62sdxhrjmtprse2r7sfq58mj3kv6tmh4f4xk4qfspe5qwcc3rxhp4ef2j0n22kg8fy0htd5q7umrrquek50g4tfx8vhyklphr2lg2nzqfnc6sxsp0k23z",
			uvk.ViewingKey);
	}

	[Fact(Skip = "Not yet implemented.")]
	public void Create_Transparent()
	{
	}

	[Fact(Skip = "Not yet implemented.")]
	public void Create_Orchard_Sapling_Transparent()
	{
	}

	[Fact]
	public void Create_IVK_Empty()
	{
		Assert.Throws<ArgumentException>(() => UnifiedViewingKey.Create((IReadOnlyCollection<IIncomingViewingKey>)Array.Empty<IIncomingViewingKey>()));
		Assert.Throws<ArgumentException>(() => UnifiedViewingKey.Create(Array.Empty<IIncomingViewingKey>()));
	}

	[Fact]
	public void Create_FVK_Empty()
	{
		Assert.Throws<ArgumentException>(() => UnifiedViewingKey.Create((IReadOnlyCollection<IFullViewingKey>)Array.Empty<IFullViewingKey>()));
		Assert.Throws<ArgumentException>(() => UnifiedViewingKey.Create(Array.Empty<IFullViewingKey>()));
	}

	[Fact]
	public void Create_IVK_Null()
	{
		Assert.Throws<ArgumentNullException>(() => UnifiedViewingKey.Create((IReadOnlyCollection<IIncomingViewingKey>)null!));
		Assert.Throws<ArgumentNullException>(() => UnifiedViewingKey.Create((IIncomingViewingKey[])null!));
	}

	[Fact]
	public void Create_FVK_Null()
	{
		Assert.Throws<ArgumentNullException>(() => UnifiedViewingKey.Create((IReadOnlyCollection<IFullViewingKey>)null!));
		Assert.Throws<ArgumentNullException>(() => UnifiedViewingKey.Create((IFullViewingKey[])null!));
	}

	[Theory]
	[InlineData("abc")]
	[InlineData("")]
	public void TryParse_BadInputs(string key)
	{
		Assert.False(UnifiedViewingKey.TryParse(key, out UnifiedViewingKey? result));
		Assert.Null(result);

		InvalidKeyException ex = Assert.Throws<InvalidKeyException>(() => UnifiedViewingKey.Parse(key));
		this.logger.WriteLine(ex.Message);
	}

	[Fact]
	public void TryParse_Null()
	{
		UnifiedViewingKey? result = null;
		Assert.Throws<ArgumentNullException>(() => UnifiedViewingKey.TryParse(null!, out result));
		Assert.Null(result);
		Assert.Throws<ArgumentNullException>(() => UnifiedViewingKey.Parse(null!));
	}

	[Theory, PairwiseData]
	public void OrchardRoundtrip(ZcashNetwork network, bool isFullViewingKey)
	{
		Zip32HDWallet wallet = new(Mnemonic, network);
		OrchardSK account = wallet.CreateOrchardAccount();
		UnifiedViewingKey uvk = UnifiedViewingKey.Create(isFullViewingKey ? account.FullViewingKey : account.IncomingViewingKey);
		AssertRoundtrip(uvk);
	}

	[Theory, PairwiseData]
	public void SaplingRoundtrip(ZcashNetwork network, bool isFullViewingKey)
	{
		Zip32HDWallet wallet = new(Mnemonic, network);
		SaplingSK account = wallet.CreateSaplingAccount();
		UnifiedViewingKey uvk = UnifiedViewingKey.Create(isFullViewingKey ? account.FullViewingKey : account.IncomingViewingKey);
		AssertRoundtrip(uvk);
	}

	[Fact]
	public void Parse_GetViewingKey()
	{
		UnifiedViewingKey uvk = UnifiedViewingKey.Parse("uview1tz7evwpdc274ekw8a7pej527wpxmchsv0hj7g65fhjgpsvzjzc3qhe79qea74c7repnc6mya6wdkawl6chk0vrx4u9dxfwhd9kl9l8k48qvy7tjtuxc4wzc0ety3t0r4p9mz88w2736m4l9r7d7t8hhj92wdxcgaukqkxmnchpn45zn5pwdmd99q6msfv7dglgqpkq95rgglsmklr7quc27xhy03fs2nha4xuufzns3glh4560tccrm739pqh6sfs33m8d50gyv5jshyra9uwktf62sdxhrjmtprse2r7sfq58mj3kv6tmh4f4xk4qfspe5qwcc3rxhp4ef2j0n22kg8fy0htd5q7umrrquek50g4tfx8vhyklphr2lg2nzqfnc6sxsp0k23z");

		SaplingFVK? sapling = uvk.GetViewingKey<SaplingFVK>();
		Assert.NotNull(sapling);
		Assert.NotNull(sapling.IncomingViewingKey);
		Assert.Equal(
			"zs1duqpcc2ql7zfjttdm2gpawe8t5ecek5k834u9vdg4mqhw7j8j39sgjy8xguvk2semyd4ujeyj28",
			sapling.IncomingViewingKey.DefaultAddress);

		OrchardFVK? orchard = uvk.GetViewingKey<OrchardFVK>();
		Assert.NotNull(orchard);
		Assert.Equal(
			"u1zpfqm4r0cc5ttvt4mft6nvyqe3uwsdcgx65s44sd3ar42rnkz7v9az0ez7dpyxvjcyj9x0sd89yy7635vn8fplwvg6vn4tr6wqpyxqaw",
			orchard.IncomingViewingKey.DefaultAddress);
	}

	[Fact]
	public void Create_RetainsViewingKeys()
	{
		Zip32HDWallet wallet = new(Mnemonic, ZcashNetwork.MainNet);
		SaplingSK saplingSK = wallet.CreateSaplingAccount();
		OrchardSK orchardSK = wallet.CreateOrchardAccount();

		UnifiedViewingKey uvk = UnifiedViewingKey.Create(saplingSK.FullViewingKey, orchardSK.FullViewingKey);

		SaplingFVK? sapling = uvk.GetViewingKey<SaplingFVK>();
		Assert.NotNull(sapling);
		Assert.Equal(saplingSK.FullViewingKey, sapling);

		OrchardFVK? orchard = uvk.GetViewingKey<OrchardFVK>();
		Assert.NotNull(orchard);
		Assert.Equal(orchardSK.FullViewingKey, orchard);
	}

	private static void AssertRoundtrip(UnifiedViewingKey uvk)
	{
		UnifiedViewingKey reparsed = UnifiedViewingKey.Parse(uvk);
		Assert.Equal(uvk.Network, reparsed.Network);
		Assert.Equal(uvk.GetType(), reparsed.GetType());
		Assert.Equal(uvk.ViewingKey, reparsed.ViewingKey);

		Assert.Equal(uvk, reparsed);
	}
}
