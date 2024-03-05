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

		this.logger.WriteLine(uvk.TextEncoding);
		Assert.Equal(
			"uview1nxgslanzvfhf0g8mzvrauh9wxedz6cmgh047wl50hzde8klctku6vcjlh4wmj3yn2c5yeh3pkyzzxyrg95r66r3pvmmc3zww6jazmznz7srvf70paklyvzfzaesxtwtfyznjwm7xpp7s2an94nh6eh3zjtfd307fvut8p48puky8sjvlw90th4cnhwez2",
			uvk.TextEncoding);
	}

	[Fact]
	public void Create_Orchard_IVK()
	{
		Zip32HDWallet wallet = new(Mnemonic, ZcashNetwork.MainNet);
		OrchardSK orchard = wallet.CreateOrchardAccount(0);
		UnifiedViewingKey.Incoming uvk = Assert.IsType<UnifiedViewingKey.Incoming>(UnifiedViewingKey.Incoming.Create(orchard.IncomingViewingKey));

		this.logger.WriteLine(uvk.TextEncoding);
		Assert.Equal(
			"uivk1n6fsvfna88p0nwz52ewkue0d2u0z0lhxxwfnx5r28gf0gnwfj2td6jkjpkw0840n9jn05ytg4422jckxspn0fnhseydqv08quk7a94wwhyqtq96ekan5exzj7qy0y2kjwmqqxyfdk6",
			uvk.TextEncoding);
	}

	[Fact]
	public void Create_Sapling_Full()
	{
		Zip32HDWallet wallet = new(Mnemonic, ZcashNetwork.MainNet);
		SaplingSK sapling = wallet.CreateSaplingAccount(0);
		UnifiedViewingKey.Full uvk = UnifiedViewingKey.Full.Create(sapling.FullViewingKey);

		this.logger.WriteLine(uvk.TextEncoding);
		Assert.Equal(
			"uview1rhqcywmxw5jxysswyp0wy3umgj595emtz2uzl55nh3ymy2t70c35w8me6rd6gs0azd32zcwfyl0etq3plljz7am04azvm62020tlhvafcpyhhldugnad504tk8ny6aadxlq3q9rkap5nl8x6y3vsv9uauahfeale7lrc4l0vkk7v82fw6xdqklgzgage8kq8sa6etcrxxhudzndgcg93xwnnnqfld4y4vq0eu902s5zrnd7z",
			uvk.TextEncoding);
	}

	[Fact]
	public void Create_Sapling_IVK()
	{
		Zip32HDWallet wallet = new(Mnemonic, ZcashNetwork.MainNet);
		SaplingSK sapling = wallet.CreateSaplingAccount(0);
		UnifiedViewingKey.Incoming uvk = Assert.IsType<UnifiedViewingKey.Incoming>(UnifiedViewingKey.Incoming.Create(sapling.IncomingViewingKey));

		this.logger.WriteLine(uvk.TextEncoding);
		Assert.Equal(
			"uivk1620d3chszq58awdklvn7fq7zp4aq2e28y8eq2h6gypse4z836e9ycv3h5sgcs9xxwdud857fcs7gt40p92zq2k80lamykpl3mzus4v3f0jwlkzw9d20g4xs7srm9cmcj9z0qfdv56t",
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
		this.logger.WriteLine(uvk.TextEncoding);
		Assert.Equal(
			"uview12cckhde70emcxuc39fel4m0y2usrygvjdef7lclnzh0s088tpy4vy6ul7v8qeqh2dnjvkvtxdvydvxy2pu035fs4g8drhm74sv6nw9f498tunf9saz4z6ct7u9w2wl80ddl89uxqzrfn0g4pzjv9czr2a2fjn796a60l402hzl9an5wl8hn0jxs8ls4xplc6mwj2zsfddpvxfw24wwwxhqpu9tyyfadduz75ec3j8j40uh0zxqdeepl9nu9hv7vnyc68qvgsfvakae7x22xgtmwj4j7zdg65y7wgt2y5l2jvpec09cwf5up4n0m6u6nr0w7hjc97e6xrq6ed6udszdh7m2ygzfsc8fcxfgxahju0v9hqt9zrwjg0397h9f4p56pt5xsjj2rwt",
			uvk.TextEncoding);
	}

	[Fact]
	public void Create_IVK_Orchard_Sapling()
	{
		Zip32HDWallet wallet = new(Mnemonic, ZcashNetwork.MainNet);
		OrchardSK orchard = wallet.CreateOrchardAccount(0);
		SaplingSK sapling = wallet.CreateSaplingAccount(0);
		UnifiedViewingKey.Incoming uvk = UnifiedViewingKey.Incoming.Create(orchard.IncomingViewingKey, sapling.IncomingViewingKey);

		this.logger.WriteLine(uvk.TextEncoding);
		Assert.Equal(
			"uivk1nh8kauhwrv3gstkyv8zmzkee9s9pru3nnxjx59yd4j7vknqem0qeavzl2mqdw57glepett06n9jyl9pxhcuez309np46y68fhmfwcge3je00nc5azsymwpjfczjleqkmh5kmpqczf9fea73xkt73vy5epgvwtjt5wusafzkkplyfjfl6g8e9r796kxdpek2wvk25clufh60lfalv3f0wpndlpftqntqsqnkf0kp688yqkjdu4da",
			uvk.TextEncoding);
	}

	[Fact]
	public void Create_Transparent_FVK()
	{
		Zip32HDWallet wallet = new(Mnemonic, ZcashNetwork.MainNet);
		TransparentSK transparent = wallet.CreateTransparentAccount(0);
		UnifiedViewingKey.Full uvk = UnifiedViewingKey.Full.Create(transparent);
		Assert.Equal(1, uvk.Revision);

		this.logger.WriteLine(uvk.TextEncoding);
		Assert.Equal(
			"urview1vnzjegxd057a8x2zx96ujjgg7fymdlq5qren6yhldtnsyne60qnltxj4kx9fd9wpyqcnympdtw405f86mdnrkaw7cgz949h3tmvpqrar23nrdvswmcxm2dh6hzt4t4kvcyyuvayc7ap",
			uvk.TextEncoding);
	}

	[Fact]
	public void Create_Transparent_IVK()
	{
		Zip32HDWallet wallet = new(Mnemonic, ZcashNetwork.MainNet);
		TransparentFVK transparent = wallet.CreateTransparentAccount(0).FullViewingKey;
		UnifiedViewingKey.Incoming uvk = UnifiedViewingKey.Incoming.Create(transparent);

		this.logger.WriteLine(uvk.TextEncoding);
		Assert.Equal(
			"urivk1ara39tyfhuydtfvy7094q4aapjh0crswnm70k4erhcclv9tn3z0n23jr4nndz8qsfverradlnklnneja4zm44cagvqf90ytqxaedk5mafef9j55uzmmcavg8vwzjn0hzllrlcpcqhlp",
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

		this.logger.WriteLine(uvk.TextEncoding);
		Assert.Equal(
			"uview12cckhde70emcxuc39fel4m0y2usrygvjdef7lclnzh0s088tpy4vy6ul7v8qeqh2dnjvkvtxdvydvxy2pu035fs4g8drhm74sv6nw9f498tunf9saz4z6ct7u9w2wl80ddl89uxqzrfn0g4pzjv9czr2a2fjn796a60l402hzl9an5wl8hn0jxs8ls4xplc6mwj2zsfddpvxfw24wwwxhqpu9tyyfadduz75ec3j8j40uh0zxqdeepl9nu9hv7vnyc68qvgsfvakae7x22xgtmwj4j7zdg65y7wgt2y5l2jvpec09cwf5up4n0m6u6nr0w7hjc97e6xrq6ed6udszdh7m2ygzfsc8fcxfgxahju0v9hqt9zrwjg0397h9f4p56pt5xsjj2rwt",
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
		DiversifierIndex index = default;
		Assert.True(UnifiedAddress.TryCreate(ref index, [saplingSK, orchardSK], out UnifiedAddress? ua));

		UnifiedViewingKey.Full ufvk = UnifiedViewingKey.Full.Create(saplingSK, orchardSK);
		Assert.Equal(ua, ufvk.DefaultAddress);
		this.logger.WriteLine(ua);
	}

	[Fact]
	public void Metadata_Propagates_FVK_IVK_Address()
	{
		Zip32HDWallet wallet = new(Mnemonic, ZcashNetwork.MainNet);
		SaplingSK saplingSK = wallet.CreateSaplingAccount();

		UnifiedEncodingMetadata metadata = new()
		{
			ExpirationDate = DateTimeOffset.UtcNow.AddDays(30),
			ExpirationHeight = 1_000_000,
		};

		UnifiedViewingKey.Full ufvk = UnifiedViewingKey.Full.Create([saplingSK.FullViewingKey], metadata);
		Assert.Equal(metadata, ufvk.Metadata);
		Assert.Equal(metadata, UnifiedViewingKey.Decode(ufvk.TextEncoding).Metadata);

		Assert.Equal(metadata, ufvk.IncomingViewingKey.Metadata);
		Assert.Equal(metadata, UnifiedViewingKey.Decode(ufvk.IncomingViewingKey.TextEncoding).Metadata);

		Assert.Equal(metadata, ufvk.DefaultAddress.Metadata);
		Assert.Equal(metadata, ufvk.IncomingViewingKey.DefaultAddress.Metadata);
	}

	[Fact]
	public void Revision_Propagates_FVK_IVK_Address()
	{
		// This encoding is revision 1 but contains no metadata, so it COULD be revision 0.
		UnifiedViewingKey.Full ufvk = (UnifiedViewingKey.Full)UnifiedViewingKey.Decode("urview1qxsmd0jqtxp945955ve5mj0yh2p83gk38zayjr7edghjx694uvv9r5m4meuhkfh59edx0vzf5yaq8lr7yet5yfnckj76g9rfmtpfsq99nrs65t2d92ywgxm7p4c7pjx39gasz76yta4l30ccg6d9pq9uzgyfmvyxjpsssxkqw5ks3axwj4q5wftptt8hzrj8umpl30pfz67zs67pcjfllj0tp6s6zl3mnr0sf756kyfjxug3");
		Assert.Equal(1, ufvk.Revision);
		Assert.Equal(UnifiedEncodingMetadata.Default, ufvk.Metadata);

		// Assert that deriving an IUVK from revision 1 UFVK will produce a revision 1 IUVK.
		this.logger.WriteLine(ufvk.IncomingViewingKey.TextEncoding);
		Assert.Equal(ufvk.Revision, ufvk.IncomingViewingKey.Revision);
		Assert.Equal(UnifiedEncodingMetadata.Default, ufvk.IncomingViewingKey.Metadata);

		// Assert that deriving a UA from a rev. 1 IUVK produces a rev. 1 UA.
		Assert.Equal(ufvk.IncomingViewingKey.Revision, ufvk.IncomingViewingKey.DefaultAddress.Revision);
		Assert.StartsWith("ur1", ufvk.IncomingViewingKey.DefaultAddress.Address);
	}

	[Fact]
	public void Create_FVK_Metadata()
	{
		Zip32HDWallet wallet = new(Mnemonic, ZcashNetwork.MainNet);
		SaplingSK saplingSK = wallet.CreateSaplingAccount();

		UnifiedEncodingMetadata metadata = new()
		{
			ExpirationDate = DateTimeOffset.UtcNow.AddDays(30),
			ExpirationHeight = 1_000_000,
		};

		UnifiedViewingKey.Full ufvk = UnifiedViewingKey.Full.Create([saplingSK.FullViewingKey], metadata);
		this.logger.WriteLine(ufvk);
		Assert.Equal(metadata, ufvk.Metadata);
	}

	[Fact]
	public void Create_IVK_Metadata()
	{
		Zip32HDWallet wallet = new(Mnemonic, ZcashNetwork.MainNet);
		SaplingSK saplingSK = wallet.CreateSaplingAccount();

		UnifiedEncodingMetadata metadata = new()
		{
			ExpirationDate = DateTimeOffset.UtcNow.AddDays(30),
			ExpirationHeight = 1_000_000,
		};

		UnifiedViewingKey.Incoming uivk = UnifiedViewingKey.Incoming.Create([saplingSK.FullViewingKey], metadata);
		Assert.Equal(metadata, uivk.Metadata);
	}

	[Fact]
	public void Equals_ConsidersMetadata()
	{
		Zip32HDWallet wallet = new(Mnemonic, ZcashNetwork.MainNet);
		SaplingSK saplingSK = wallet.CreateSaplingAccount();

		UnifiedEncodingMetadata metadata = new()
		{
			ExpirationDate = DateTimeOffset.UtcNow.AddDays(30),
			ExpirationHeight = 1_000_000,
		};

		UnifiedEncodingMetadata metadataCopy = new()
		{
			ExpirationDate = metadata.ExpirationDate,
			ExpirationHeight = 1_000_000,
		};

		UnifiedViewingKey.Full ufvkWithMetadata = UnifiedViewingKey.Full.Create([saplingSK.FullViewingKey], metadata);
		UnifiedViewingKey.Full ufvkWithMetadata2 = UnifiedViewingKey.Full.Create([saplingSK.FullViewingKey], metadataCopy);
		UnifiedViewingKey.Full ufvk = UnifiedViewingKey.Full.Create([saplingSK.FullViewingKey]);

		Assert.True(ufvkWithMetadata2.Equals(ufvkWithMetadata));
		Assert.False(ufvkWithMetadata.Equals(ufvk));
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
