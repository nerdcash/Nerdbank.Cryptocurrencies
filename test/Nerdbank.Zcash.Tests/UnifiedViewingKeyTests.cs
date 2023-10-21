// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using OrchardFVK = Nerdbank.Zcash.Orchard.FullViewingKey;
using OrchardIVK = Nerdbank.Zcash.Orchard.IncomingViewingKey;
using OrchardSK = Nerdbank.Zcash.Zip32HDWallet.Orchard.ExtendedSpendingKey;
using SaplingFVK = Nerdbank.Zcash.Sapling.DiversifiableFullViewingKey;
using SaplingIVK = Nerdbank.Zcash.Sapling.DiversifiableIncomingViewingKey;
using SaplingSK = Nerdbank.Zcash.Zip32HDWallet.Sapling.ExtendedSpendingKey;
using TransparentFVK = Nerdbank.Zcash.Zip32HDWallet.Transparent.ExtendedViewingKey;
using TransparentSK = Nerdbank.Zcash.Zip32HDWallet.Transparent.ExtendedSpendingKey;

public class UnifiedViewingKeyTests : TestBase
{
	private readonly ITestOutputHelper logger;

	public UnifiedViewingKeyTests(ITestOutputHelper logger)
	{
		this.logger = logger;
	}

	[Fact]
	public void Create_FVK_NonUniqueTypes()
	{
		Zip32HDWallet wallet = new(Mnemonic, ZcashNetwork.TestNet);
		OrchardSK account1 = wallet.CreateOrchardAccount(0);
		OrchardSK account2 = wallet.CreateOrchardAccount(1);

		ArgumentException ex = Assert.Throws<ArgumentException>(
			() => UnifiedViewingKey.Full.Create(new[] { account1.FullViewingKey, account2.FullViewingKey }));
		this.logger.WriteLine(ex.Message);
	}

	[Fact]
	public void Create_IVK_NonUniqueTypes()
	{
		Zip32HDWallet wallet = new(Mnemonic, ZcashNetwork.TestNet);
		OrchardSK account1 = wallet.CreateOrchardAccount(0);
		OrchardSK account2 = wallet.CreateOrchardAccount(1);

		ArgumentException ex = Assert.Throws<ArgumentException>(
			() => UnifiedViewingKey.Incoming.Create(new[] { account1.IncomingViewingKey, account2.IncomingViewingKey }));
		this.logger.WriteLine(ex.Message);
	}

	[Fact]
	public void Create_FVK_MixedNetworks()
	{
		Zip32HDWallet testWallet = new(Mnemonic, ZcashNetwork.TestNet);
		Zip32HDWallet mainWallet = new(Mnemonic, ZcashNetwork.MainNet);
		OrchardSK orchard = testWallet.CreateOrchardAccount(0);
		SaplingSK sapling = mainWallet.CreateSaplingAccount(0);
		ArgumentException ex = Assert.Throws<ArgumentException>(
			() => UnifiedViewingKey.Full.Create(orchard.FullViewingKey, sapling.FullViewingKey));
		this.logger.WriteLine(ex.Message);
	}

	[Fact]
	public void Create_IVK_MixedNetworks()
	{
		Zip32HDWallet testWallet = new(Mnemonic, ZcashNetwork.TestNet);
		Zip32HDWallet mainWallet = new(Mnemonic, ZcashNetwork.MainNet);
		OrchardSK orchard = testWallet.CreateOrchardAccount(0);
		SaplingSK sapling = mainWallet.CreateSaplingAccount(0);
		ArgumentException ex = Assert.Throws<ArgumentException>(
			() => UnifiedViewingKey.Incoming.Create(orchard.IncomingViewingKey, sapling.IncomingViewingKey));
		this.logger.WriteLine(ex.Message);
	}

	[Fact]
	public void Create_MixedViewingTypes()
	{
		Zip32HDWallet wallet = new(Mnemonic, ZcashNetwork.TestNet);
		OrchardSK orchard = wallet.CreateOrchardAccount(0);
		SaplingSK sapling = wallet.CreateSaplingAccount(0);
		UnifiedViewingKey.Incoming uivk = UnifiedViewingKey.Incoming.Create(orchard.FullViewingKey, sapling.IncomingViewingKey);
		this.logger.WriteLine(uivk.TextEncoding);
		Assert.StartsWith("uivktest1", uivk.TextEncoding);
	}

	[Fact]
	public void ToString_FVK_ReturnsEncoding()
	{
		Zip32HDWallet wallet = new(Mnemonic, ZcashNetwork.MainNet);
		OrchardSK orchard = wallet.CreateOrchardAccount(0);
		UnifiedViewingKey uvk = UnifiedViewingKey.Full.Create(orchard.FullViewingKey);
		Assert.Equal(uvk.TextEncoding, uvk.ToString());
	}

	[Fact]
	public void ToString_IVK_ReturnsEncoding()
	{
		Zip32HDWallet wallet = new(Mnemonic, ZcashNetwork.MainNet);
		OrchardSK orchard = wallet.CreateOrchardAccount(0);
		UnifiedViewingKey uvk = UnifiedViewingKey.Incoming.Create(orchard.IncomingViewingKey);
		Assert.Equal(uvk.TextEncoding, uvk.ToString());
	}

	[Fact]
	public void ImplicitCastToString_FVK_ReturnsEncoding()
	{
		Zip32HDWallet wallet = new(Mnemonic, ZcashNetwork.MainNet);
		OrchardSK orchard = wallet.CreateOrchardAccount(0);
		UnifiedViewingKey.Full uvk = UnifiedViewingKey.Full.Create(orchard.FullViewingKey);
		string uvkString = uvk;
		Assert.Equal(uvk.TextEncoding, uvkString);
	}

	[Fact]
	public void ImplicitCastToString_IVK_ReturnsEncoding()
	{
		Zip32HDWallet wallet = new(Mnemonic, ZcashNetwork.MainNet);
		OrchardSK orchard = wallet.CreateOrchardAccount(0);
		UnifiedViewingKey.Incoming uvk = UnifiedViewingKey.Incoming.Create(orchard.IncomingViewingKey);
		string uvkString = uvk;
		Assert.Equal(uvk.TextEncoding, uvkString);
	}

	[Fact]
	public void Create_Orchard_FVK()
	{
		Zip32HDWallet wallet = new(Mnemonic, ZcashNetwork.MainNet);
		OrchardSK orchard = wallet.CreateOrchardAccount(0);
		UnifiedViewingKey.Full uvk = UnifiedViewingKey.Full.Create(orchard.FullViewingKey);

		Assert.Equal(
			"uview12z0pgg2u7q5ky5wzas8mmgcs4zy8cmdyt62tn3ecpdurnqqnlldx6j73600qe4xkz7jp4w37elr2d48jm5ktuvlm5x8z5ke6cg3x8m6sk5sruh4xnjk93h86fls0uyhhtaj8vu0mw0t7cr74vc8ra2360yhamnskk7a7ahsasndmagsmuhs27lqdyjsz9",
			uvk.TextEncoding);
	}

	[Fact]
	public void Create_Orchard_IVK()
	{
		Zip32HDWallet wallet = new(Mnemonic, ZcashNetwork.MainNet);
		OrchardSK orchard = wallet.CreateOrchardAccount(0);
		UnifiedViewingKey.Incoming uvk = Assert.IsType<UnifiedViewingKey.Incoming>(UnifiedViewingKey.Incoming.Create(orchard.IncomingViewingKey));

		Assert.Equal(
			"uivk1zz6k7d37rldq7mk0n4uegyvesggreucz8h48nnrvlzznhvv3kqm4zp7wngksclaptu8hfqc6l57takh5x6jypygqp5m7d36gmxlxakgzqkp3luqrdgnk97k556wezjydjkmqkvkvf6",
			uvk.TextEncoding);
	}

	[Fact]
	public void Create_Sapling_Full()
	{
		Zip32HDWallet wallet = new(Mnemonic, ZcashNetwork.MainNet);
		SaplingSK sapling = wallet.CreateSaplingAccount(0);
		UnifiedViewingKey.Full uvk = UnifiedViewingKey.Full.Create(sapling.FullViewingKey);

		Assert.Equal(
			"uview17x4ug5kp5shmrywnsadcce6dmyj54ey9hgu4yek25zfw5vnhrghectdsdgsgc099jj99amrxkl5afvyv50jxpwg3u53rq4hzhtln8gurtu5vey602wp0q6xcyxnjtat0a5hn4z82am7fygd42u4h3n27ndtpkx9w6p8nv20k7f5swak7g4wvhte39sasxm9rxea3y7q3c537csxlut9jaf8g3j3ddl02x4j0j7zg0qglvx55",
			uvk.TextEncoding);
	}

	[Fact]
	public void Create_Sapling_IVK()
	{
		Zip32HDWallet wallet = new(Mnemonic, ZcashNetwork.MainNet);
		SaplingSK sapling = wallet.CreateSaplingAccount(0);
		UnifiedViewingKey.Incoming uvk = Assert.IsType<UnifiedViewingKey.Incoming>(UnifiedViewingKey.Incoming.Create(sapling.IncomingViewingKey));

		Assert.Equal(
			"uivk1fahcymtzgy7kza5nvvq9s0wjxsgjhwt5aunczjeg2sszfrutf0gdrfue9en54ecmlzfxr7hsfwyaqzygzf8f35ng6za3jzxfceh2t093asl04t299wca03ezeuchn4h2cf5s66zdzr",
			uvk.TextEncoding);
	}

	[Fact]
	public void Create_FVK_Orchard_Sapling()
	{
		Zip32HDWallet wallet = new(Mnemonic, ZcashNetwork.MainNet);
		OrchardSK orchard = wallet.CreateOrchardAccount(0);
		SaplingSK sapling = wallet.CreateSaplingAccount(0);
		UnifiedViewingKey.Full uvk = UnifiedViewingKey.Full.Create(orchard.FullViewingKey, sapling.FullViewingKey);

		// This expected value came from YWallet when generated with the test seed phrase.
		Assert.Equal(
			"uview1tz7evwpdc274ekw8a7pej527wpxmchsv0hj7g65fhjgpsvzjzc3qhe79qea74c7repnc6mya6wdkawl6chk0vrx4u9dxfwhd9kl9l8k48qvy7tjtuxc4wzc0ety3t0r4p9mz88w2736m4l9r7d7t8hhj92wdxcgaukqkxmnchpn45zn5pwdmd99q6msfv7dglgqpkq95rgglsmklr7quc27xhy03fs2nha4xuufzns3glh4560tccrm739pqh6sfs33m8d50gyv5jshyra9uwktf62sdxhrjmtprse2r7sfq58mj3kv6tmh4f4xk4qfspe5qwcc3rxhp4ef2j0n22kg8fy0htd5q7umrrquek50g4tfx8vhyklphr2lg2nzqfnc6sxsp0k23z",
			uvk.TextEncoding);
	}

	[Fact]
	public void Create_IVK_Orchard_Sapling()
	{
		Zip32HDWallet wallet = new(Mnemonic, ZcashNetwork.MainNet);
		OrchardSK orchard = wallet.CreateOrchardAccount(0);
		SaplingSK sapling = wallet.CreateSaplingAccount(0);
		UnifiedViewingKey.Incoming uvk = UnifiedViewingKey.Incoming.Create(orchard.IncomingViewingKey, sapling.IncomingViewingKey);

		Assert.Equal(
			"uivk14pdvka3fgede8n5atxw3rq5h55w4qqyte3n8zkzhh8m92qju0kewjz3jzklnx99tu6u9lwye2a4gvks66sdkzrd2q67zqn7lwfejvqh9g32dad8mqy2hx7ug9ak6qryuq8u055a090s9mwaw7l86awhnrqkkgu4u8updz2rvuqn08x9efhtwmknl327a4s3akm2mv9qe2h9j788zmptyxsctmthdw8gngd2svmsex548s97ypjg",
			uvk.TextEncoding);
	}

	[Fact]
	public void Create_Transparent_FVK()
	{
		Zip32HDWallet wallet = new(Mnemonic, ZcashNetwork.MainNet);
		TransparentSK transparent = wallet.CreateTransparentAccount(0);
		UnifiedViewingKey.Full uvk = UnifiedViewingKey.Full.Create(transparent);

		this.logger.WriteLine(uvk);
		Assert.Equal(
			"uview1ntk3zd44nje0tz4x6ml8vxxse88dnsts9c3mdppgu3qk8gny27qrvjk38y9htmv0f5my8qp0nwpcepdpmqeat7gg9kux8jtkzl7nap73sptf4vcg03vgj94qd9kafm2q2mss69kpqy2",
			uvk.TextEncoding);
	}

	[Fact]
	public void Create_Transparent_IVK()
	{
		Zip32HDWallet wallet = new(Mnemonic, ZcashNetwork.MainNet);
		TransparentFVK transparent = wallet.CreateTransparentAccount(0).FullViewingKey;
		UnifiedViewingKey.Incoming uvk = UnifiedViewingKey.Incoming.Create(transparent);

		this.logger.WriteLine(uvk);
		Assert.Equal(
			"uivk1rc6aa5kgpfgltsd2ds9gxggs3rpfayed6rr8mwnjk3hces69cgdv7p5nqvfe95fwy7kdfvs58z7er8kyyznezgnaky8jk9tx57zk4qzudm06tq9pglzfja4phcs7mu407m595ucetr2",
			uvk.TextEncoding);
	}

	[Fact]
	public void Create_FVK_Orchard_Sapling_Transparent()
	{
		Zip32HDWallet wallet = new(Mnemonic, ZcashNetwork.MainNet);
		OrchardSK orchard = wallet.CreateOrchardAccount(0);
		SaplingSK sapling = wallet.CreateSaplingAccount(0);
		TransparentSK transparent = wallet.CreateTransparentAccount(0);
		UnifiedViewingKey.Full uvk = UnifiedViewingKey.Full.Create(orchard.FullViewingKey, sapling.FullViewingKey);

		Assert.Equal(
			"uview1tz7evwpdc274ekw8a7pej527wpxmchsv0hj7g65fhjgpsvzjzc3qhe79qea74c7repnc6mya6wdkawl6chk0vrx4u9dxfwhd9kl9l8k48qvy7tjtuxc4wzc0ety3t0r4p9mz88w2736m4l9r7d7t8hhj92wdxcgaukqkxmnchpn45zn5pwdmd99q6msfv7dglgqpkq95rgglsmklr7quc27xhy03fs2nha4xuufzns3glh4560tccrm739pqh6sfs33m8d50gyv5jshyra9uwktf62sdxhrjmtprse2r7sfq58mj3kv6tmh4f4xk4qfspe5qwcc3rxhp4ef2j0n22kg8fy0htd5q7umrrquek50g4tfx8vhyklphr2lg2nzqfnc6sxsp0k23z",
			uvk.TextEncoding);
	}

	/// <summary>
	/// Verifies that we get an encoding that does <em>not</em> carry outgoing view keys
	/// even if the input contained them.
	/// </summary>
	[Fact]
	public void Create_IVK_WithFullKeyInputs()
	{
		Zip32HDWallet wallet = new(Mnemonic, ZcashNetwork.MainNet);
		OrchardSK orchard = wallet.CreateOrchardAccount(0);
		SaplingSK sapling = wallet.CreateSaplingAccount(0);
		UnifiedViewingKey.Incoming uivk = UnifiedViewingKey.Incoming.Create(orchard.FullViewingKey, sapling.FullViewingKey);

		AssertNoOutgoingKey(uivk);
	}

	/// <summary>
	/// Verifies that we get an encoding that does <em>not</em> carry outgoing view keys
	/// even if the input contained them.
	/// </summary>
	[Fact]
	public void Create_FVK_WithSpendingKeyInputs()
	{
		Zip32HDWallet wallet = new(Mnemonic, ZcashNetwork.MainNet);
		OrchardSK orchard = wallet.CreateOrchardAccount(0);
		SaplingSK sapling = wallet.CreateSaplingAccount(0);
		UnifiedViewingKey.Full ufvk = UnifiedViewingKey.Full.Create(orchard, sapling);

		AssertNoSpendingKey(ufvk);
	}

	[Fact]
	public void FullToIncomingViewingKey()
	{
		Zip32HDWallet wallet = new(Mnemonic, ZcashNetwork.MainNet);
		OrchardSK orchard = wallet.CreateOrchardAccount(0);
		SaplingSK sapling = wallet.CreateSaplingAccount(0);
		UnifiedViewingKey.Full ufvk = UnifiedViewingKey.Full.Create(orchard.FullViewingKey, sapling.FullViewingKey);
		this.logger.WriteLine(ufvk);
		UnifiedViewingKey.Incoming uivk = ufvk.IncomingViewingKey;
		this.logger.WriteLine(uivk);

		AssertNoOutgoingKey(uivk);
	}

	[Fact]
	public void Create_IVK_Empty()
	{
		Assert.Throws<ArgumentException>(() => UnifiedViewingKey.Incoming.Create((IReadOnlyCollection<IIncomingViewingKey>)Array.Empty<IIncomingViewingKey>()));
		Assert.Throws<ArgumentException>(() => UnifiedViewingKey.Incoming.Create(Array.Empty<IIncomingViewingKey>()));
	}

	[Fact]
	public void Create_FVK_Empty()
	{
		Assert.Throws<ArgumentException>(() => UnifiedViewingKey.Full.Create((IReadOnlyCollection<IFullViewingKey>)Array.Empty<IFullViewingKey>()));
		Assert.Throws<ArgumentException>(() => UnifiedViewingKey.Full.Create(Array.Empty<IFullViewingKey>()));
	}

	[Fact]
	public void Create_IVK_Null()
	{
		Assert.Throws<ArgumentNullException>(() => UnifiedViewingKey.Incoming.Create((IReadOnlyCollection<IIncomingViewingKey>)null!));
		Assert.Throws<ArgumentNullException>(() => UnifiedViewingKey.Incoming.Create((IIncomingViewingKey[])null!));
	}

	[Fact]
	public void Create_FVK_Null()
	{
		Assert.Throws<ArgumentNullException>(() => UnifiedViewingKey.Full.Create((IReadOnlyCollection<IFullViewingKey>)null!));
		Assert.Throws<ArgumentNullException>(() => UnifiedViewingKey.Full.Create((IFullViewingKey[])null!));
	}

	[Theory]
	[InlineData("abc")]
	[InlineData("")]
	public void TryDecode_BadInputs(string key)
	{
		Assert.False(UnifiedViewingKey.TryDecode(key, out _, out _, out UnifiedViewingKey? result));
		Assert.Null(result);

		InvalidKeyException ex = Assert.Throws<InvalidKeyException>(() => UnifiedViewingKey.Decode(key));
		this.logger.WriteLine(ex.Message);
	}

	[Fact]
	public void TryDecode_Null()
	{
		UnifiedViewingKey? result = null;
		Assert.Throws<ArgumentNullException>(() => UnifiedViewingKey.TryDecode(null!, out _, out _, out result));
		Assert.Null(result);
		Assert.Throws<ArgumentNullException>(() => UnifiedViewingKey.Decode(null!));
	}

	[Theory, PairwiseData]
	public void OrchardRoundtrip(ZcashNetwork network, bool isFullViewingKey)
	{
		Zip32HDWallet wallet = new(Mnemonic, network);
		OrchardSK account = wallet.CreateOrchardAccount();
		UnifiedViewingKey uvk = isFullViewingKey
			? UnifiedViewingKey.Full.Create(account.FullViewingKey)
			: UnifiedViewingKey.Incoming.Create(account.IncomingViewingKey);
		AssertRoundtrip(uvk);
	}

	[Theory, PairwiseData]
	public void SaplingRoundtrip(ZcashNetwork network, bool isFullViewingKey)
	{
		Zip32HDWallet wallet = new(Mnemonic, network);
		SaplingSK account = wallet.CreateSaplingAccount();
		UnifiedViewingKey uvk = isFullViewingKey
			? UnifiedViewingKey.Full.Create(account.FullViewingKey)
			: UnifiedViewingKey.Incoming.Create(account.IncomingViewingKey);
		AssertRoundtrip(uvk);
	}

	[Theory, PairwiseData]
	public void TransparentRoundtrip(ZcashNetwork network, bool isFullViewingKey)
	{
		Zip32HDWallet wallet = new(Mnemonic, network);
		TransparentSK account = wallet.CreateTransparentAccount();
		UnifiedViewingKey uvk = isFullViewingKey
			? UnifiedViewingKey.Full.Create(account.FullViewingKey)
			: UnifiedViewingKey.Incoming.Create(account.FullViewingKey);
		AssertRoundtrip(uvk);
	}

	[Fact]
	public void Decode_GetViewingKey()
	{
		UnifiedViewingKey uvk = UnifiedViewingKey.Decode("uview1tz7evwpdc274ekw8a7pej527wpxmchsv0hj7g65fhjgpsvzjzc3qhe79qea74c7repnc6mya6wdkawl6chk0vrx4u9dxfwhd9kl9l8k48qvy7tjtuxc4wzc0ety3t0r4p9mz88w2736m4l9r7d7t8hhj92wdxcgaukqkxmnchpn45zn5pwdmd99q6msfv7dglgqpkq95rgglsmklr7quc27xhy03fs2nha4xuufzns3glh4560tccrm739pqh6sfs33m8d50gyv5jshyra9uwktf62sdxhrjmtprse2r7sfq58mj3kv6tmh4f4xk4qfspe5qwcc3rxhp4ef2j0n22kg8fy0htd5q7umrrquek50g4tfx8vhyklphr2lg2nzqfnc6sxsp0k23z");

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
	public void TryDecode()
	{
		Assert.True(TryDecodeViaInterface<UnifiedViewingKey>("uview1tz7evwpdc274ekw8a7pej527wpxmchsv0hj7g65fhjgpsvzjzc3qhe79qea74c7repnc6mya6wdkawl6chk0vrx4u9dxfwhd9kl9l8k48qvy7tjtuxc4wzc0ety3t0r4p9mz88w2736m4l9r7d7t8hhj92wdxcgaukqkxmnchpn45zn5pwdmd99q6msfv7dglgqpkq95rgglsmklr7quc27xhy03fs2nha4xuufzns3glh4560tccrm739pqh6sfs33m8d50gyv5jshyra9uwktf62sdxhrjmtprse2r7sfq58mj3kv6tmh4f4xk4qfspe5qwcc3rxhp4ef2j0n22kg8fy0htd5q7umrrquek50g4tfx8vhyklphr2lg2nzqfnc6sxsp0k23z", out DecodeError? decodeError, out string? errorMessage, out IKeyWithTextEncoding? key));
		UnifiedViewingKey uvk = (UnifiedViewingKey)key;

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
	public void Create_FVK_RetainsViewingKeys()
	{
		Zip32HDWallet wallet = new(Mnemonic, ZcashNetwork.MainNet);
		SaplingSK saplingSK = wallet.CreateSaplingAccount();
		OrchardSK orchardSK = wallet.CreateOrchardAccount();

		UnifiedViewingKey uvk = UnifiedViewingKey.Full.Create(saplingSK.FullViewingKey, orchardSK.FullViewingKey);

		SaplingFVK? sapling = uvk.GetViewingKey<SaplingFVK>();
		Assert.NotNull(sapling);
		Assert.Equal(saplingSK.FullViewingKey, sapling);

		OrchardFVK? orchard = uvk.GetViewingKey<OrchardFVK>();
		Assert.NotNull(orchard);
		Assert.Equal(orchardSK.FullViewingKey, orchard);
	}

	[Fact]
	public void Create_IVK_RetainsViewingKeys()
	{
		Zip32HDWallet wallet = new(Mnemonic, ZcashNetwork.MainNet);
		SaplingSK saplingSK = wallet.CreateSaplingAccount();
		OrchardSK orchardSK = wallet.CreateOrchardAccount();

		UnifiedViewingKey uvk = UnifiedViewingKey.Incoming.Create(saplingSK.IncomingViewingKey, orchardSK.IncomingViewingKey);

		SaplingIVK? sapling = uvk.GetViewingKey<SaplingIVK>();
		Assert.NotNull(sapling);
		Assert.Equal(saplingSK.IncomingViewingKey, sapling);

		OrchardIVK? orchard = uvk.GetViewingKey<OrchardIVK>();
		Assert.NotNull(orchard);
		Assert.Equal(orchardSK.IncomingViewingKey, orchard);
	}

	[Fact]
	public void DefaultAddress_Is_UnifiedReceivingAddress()
	{
		Zip32HDWallet wallet = new(Mnemonic, ZcashNetwork.MainNet);
		SaplingSK saplingSK = wallet.CreateSaplingAccount();
		OrchardSK orchardSK = wallet.CreateOrchardAccount();
		UnifiedAddress ua = UnifiedAddress.Create(saplingSK.DefaultAddress, orchardSK.DefaultAddress);

		UnifiedViewingKey.Full ufvk = UnifiedViewingKey.Full.Create(saplingSK, orchardSK);
		Assert.Equal(ua, ufvk.DefaultAddress);
		this.logger.WriteLine(ua);
	}

	private static void AssertNoOutgoingKey(UnifiedViewingKey uivk)
	{
		Assert.StartsWith("uivk1", uivk.TextEncoding);
		Assert.NotNull(uivk.GetViewingKey<IIncomingViewingKey>());
		Assert.Null(uivk.GetViewingKey<IFullViewingKey>());

		UnifiedViewingKey parsed = UnifiedViewingKey.Decode(uivk);
		Assert.NotNull(parsed.GetViewingKey<IIncomingViewingKey>());
		Assert.Null(parsed.GetViewingKey<IFullViewingKey>());
	}

	private static void AssertNoSpendingKey(UnifiedViewingKey uvk)
	{
		Assert.NotNull(uvk.GetViewingKey<IIncomingViewingKey>());
		Assert.Null(uvk.GetViewingKey<ISpendingKey>());

		UnifiedViewingKey parsed = UnifiedViewingKey.Decode(uvk);
		Assert.NotNull(parsed.GetViewingKey<IIncomingViewingKey>());
		Assert.Null(parsed.GetViewingKey<ISpendingKey>());
	}

	private static void AssertRoundtrip(UnifiedViewingKey uvk)
	{
		UnifiedViewingKey reparsed = UnifiedViewingKey.Decode(uvk);
		Assert.Equal(uvk.Network, reparsed.Network);
		Assert.Equal(uvk.GetType(), reparsed.GetType());
		Assert.Equal(uvk.TextEncoding, reparsed.TextEncoding);

		Assert.True(uvk.Equals(reparsed));
	}
}
