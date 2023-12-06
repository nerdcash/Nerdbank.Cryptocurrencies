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

	protected ZcashAccount DefaultAccount { get; } = new(Zip32, 0);

	protected ZcashAccount AlternateAccount { get; } = new(Zip32, 1);

	[Fact]
	public void DefaultAccountProperties()
	{
		Assert.Same(Zip32, this.DefaultAccount.HDDerivation?.Wallet);
		Assert.Equal<uint?>(1, this.AlternateAccount.HDDerivation?.AccountIndex);
		Assert.Equal<uint?>(0, this.DefaultAccount.MaxTransparentAddressIndex);

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

		Assert.Equal("utest1ryu7uryg2w4rwt3c0gdem8rwpw9lek0gmxxjw30947aptdkrhha5ehdamvrcdkatnfe99g3frywqjzkvqewm6qzn7p7h0a4mkw2cttq70s52xzvc32e88rtcdjgssrwqj037av4t0a48g3x5ruk7fvhv9c27jz4erka6nspytrvrc9qhmx22vefjs63tzsh9xqj3qequpyp4va2c3lu", this.DefaultAccount.DefaultAddress);
		Assert.Equal("uviewtest13psnxefac3rlvsp6tpy6d63qnegf4unum6605jlde55kdvs02hsk50k9ttc039jllw00jhnl2hwvq3tel06a5qrdvd6f9h0njhg3dgtlqgpakn5e9re5xuh4q73tz86exc87d2mfpwrqf8prj472heawygzv6nn6rt6tevaevv7py6ymnfu7qpah26zvqt9qxka87m3mjcansdmn8k7lqfwjzw6wg9rk2ujtece7gguyucl66z7dxjurcmt2cxak3gf78n6y7952ufrw98vww9fm4fgv2c9l7y924wu704t58eyfwfxtss02yjve2weslgdp325wxayq4h06nw3zrql677ynhndvlwtp24zh3twpadeey3ycuhh7qe0nwjs0q0gpd24stgtdwls5suf3wnn40t6ru29k0ccl6mygeg50w4zwahv3xdrnfzeujd70tlfwlp67l253v2mgghmvkvkuc8s0f65fpemx5uyzuw3yl59ycurarst0", this.DefaultAccount.FullViewing.UnifiedKey);
		Assert.Equal("uivktest1hpq6h37kxr8eccyvaefrxsdg9afxs56n748mngy4p70ngv5099vypdzw6p4h6yznfus3856hlgmnv7q0ya5cjq25ysnmr9ulgu3svlr8frw7f59w7sh2tuyqp573p7hujls8s3782z387hq3g0mcw66hwl2j9lwkkxg6gy9gd0hz36vqnmd9z3wl0d4x2pymuq68nzjk3jgcuduvxvvfk7hfv6d6ykp2l5schf7lexhe97jnh3kphxktnclxnqtg8puc4xcrk4lcgn0squ3x3hcu68u9f0uks0spat8ftycw4yxv64h8xhdlnr7kxgfzs5ggxp4wc9fxedvuhzf96jfwlcnrac", this.DefaultAccount.IncomingViewing.UnifiedKey);
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
		ZcashAccount account = new(new Zip32HDWallet(Mnemonic, ZcashNetwork.MainNet));

		DiversifierIndex index = default;
		UnifiedAddress diversified = account.GetDiversifiedAddress(ref index);

		// We happen to know that this particular wallet's sapling key doesn't produce a valid diversifier at index 0.
		Assert.NotEqual(default, index);
		this.logger.WriteLine($"Diversifier index: {index.ToBigInteger()}");

		// Use it again and verify we get the same answer.
		DiversifierIndex index2 = index;
		UnifiedAddress diversified2 = account.GetDiversifiedAddress(ref index2);

		Assert.Equal(index, index2);
		Assert.Equal(diversified, diversified2);
	}

	[Fact]
	public void TryGetDiversifierIndex_SaplingReceiver()
	{
		ZcashAccount account = new(new Zip32HDWallet(Mnemonic, ZcashNetwork.MainNet));
		DiversifierIndex expectedIndex = 50;
		UnifiedAddress diversified = account.GetDiversifiedAddress(ref expectedIndex);

		Assert.True(account.TryGetDiversifierIndex(new SaplingAddress(diversified.GetPoolReceiver<SaplingReceiver>()!.Value, diversified.Network), out DiversifierIndex? actualIndex));
		Assert.Equal(expectedIndex, actualIndex);
	}

	[Fact]
	public void TryGetDiversifierIndex_OrchardReceiver()
	{
		ZcashAccount account = new(new Zip32HDWallet(Mnemonic, ZcashNetwork.MainNet));
		DiversifierIndex expectedIndex = 50;
		UnifiedAddress diversified = account.GetDiversifiedAddress(ref expectedIndex);

		Assert.True(account.TryGetDiversifierIndex(new OrchardAddress(diversified.GetPoolReceiver<OrchardReceiver>()!.Value, diversified.Network), out DiversifierIndex? actualIndex));
		Assert.Equal(expectedIndex, actualIndex);
	}

	[Fact]
	public void TryGetDiversifierIndex_DualReceiverMatch()
	{
		ZcashAccount account = new(new Zip32HDWallet(Mnemonic, ZcashNetwork.MainNet));
		DiversifierIndex expectedIndex = 50;
		UnifiedAddress diversified = account.GetDiversifiedAddress(ref expectedIndex);

		Assert.True(account.TryGetDiversifierIndex(diversified, out DiversifierIndex? actualIndex));
		Assert.Equal(expectedIndex, actualIndex);
	}

	[Fact]
	public void TryGetDiversifierIndex_DualReceiverMismatch()
	{
		ZcashAccount account = new(new Zip32HDWallet(Mnemonic, ZcashNetwork.MainNet));

		DiversifierIndex expectedIndex1 = 50;
		UnifiedAddress diversified1 = account.GetDiversifiedAddress(ref expectedIndex1);

		DiversifierIndex expectedIndex2 = 150;
		UnifiedAddress diversified2 = account.GetDiversifiedAddress(ref expectedIndex2);

		UnifiedAddress mixedUA = UnifiedAddress.Create(
			new SaplingAddress(diversified1.GetPoolReceiver<SaplingReceiver>()!.Value, diversified1.Network),
			new OrchardAddress(diversified2.GetPoolReceiver<OrchardReceiver>()!.Value, diversified2.Network));

		Assert.False(account.TryGetDiversifierIndex(mixedUA, out DiversifierIndex? actualIndex));
		Assert.Null(actualIndex);
	}

	[Fact]
	public void GetTransparentAddress()
	{
		Assert.Equal<uint?>(0, this.DefaultAccount.MaxTransparentAddressIndex);
		HashSet<TransparentAddress> tAddrs = new();
		for (uint i = 0; i < 5; i++)
		{
			tAddrs.Add(this.DefaultAccount.GetTransparentAddress(i));

			// This should have automatically updated the tracking property.
			Assert.Equal(i, this.DefaultAccount.MaxTransparentAddressIndex);
		}

		// Verify that 5 unique addresses were generated.
		Assert.Equal(5, tAddrs.Count);
	}

	[Fact]
	public void GetTransparentAddress_WithoutTransparentKey()
	{
		Assert.True(ZcashAccount.TryImportAccount(this.DefaultAccount.Spending!.Orchard!.TextEncoding, out ZcashAccount? account));
		Assert.Null(account.MaxTransparentAddressIndex);
		Assert.Throws<InvalidOperationException>(() => account.GetTransparentAddress());
	}

	[Fact]
	public void AddressSendsToThisAccount_Unified()
	{
		Assert.True(this.DefaultAccount.AddressSendsToThisAccount(this.DefaultAccount.DefaultAddress));
		Assert.True(this.DefaultAccount.AddressSendsToThisAccount(this.DefaultAccount.GetDiversifiedAddress()));
		Assert.False(this.DefaultAccount.AddressSendsToThisAccount(this.AlternateAccount.DefaultAddress));
	}

	[Fact]
	public void AddressSendsToThisAccount_Sapling()
	{
		Assert.True(this.DefaultAccount.AddressSendsToThisAccount(this.DefaultAccount.IncomingViewing.Sapling!.DefaultAddress));
		Assert.False(this.DefaultAccount.AddressSendsToThisAccount(this.AlternateAccount.IncomingViewing.Sapling!.DefaultAddress));
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
			this.DefaultAccount.IncomingViewing.Orchard!.DefaultAddress,
			this.AlternateAccount.IncomingViewing.Sapling!.DefaultAddress);
		Assert.False(this.DefaultAccount.AddressSendsToThisAccount(unfriendly));
	}

	[Fact]
	public void FullViewingAccount()
	{
		ZcashAccount fullViewAccount = new(this.DefaultAccount.FullViewing!.UnifiedKey);

		Assert.Null(fullViewAccount.HDDerivation);

		Assert.Null(fullViewAccount.Spending);
		Assert.NotNull(fullViewAccount.FullViewing?.Transparent);
		Assert.NotNull(fullViewAccount.FullViewing?.Sapling);
		Assert.NotNull(fullViewAccount.FullViewing?.Orchard);
		Assert.Equal(this.DefaultAccount.FullViewing!.UnifiedKey, fullViewAccount.FullViewing?.UnifiedKey);
	}

	[Fact]
	public void IncomingViewingAccount()
	{
		ZcashAccount incomingViewAccount = new(this.DefaultAccount.IncomingViewing.UnifiedKey);

		Assert.Null(incomingViewAccount.Spending);
		Assert.Null(incomingViewAccount.FullViewing);
		Assert.NotNull(incomingViewAccount.IncomingViewing?.Transparent);
		Assert.NotNull(incomingViewAccount.IncomingViewing?.Sapling);
		Assert.NotNull(incomingViewAccount.IncomingViewing?.Orchard);
		Assert.Equal(this.DefaultAccount.IncomingViewing!.UnifiedKey, incomingViewAccount.IncomingViewing?.UnifiedKey);
	}

	[Fact]
	public void HasDiversifiableKeys()
	{
		// Default account has diversifiable keys.
		Assert.True(this.DefaultAccount.HasDiversifiableKeys);

		// Orchard only accounts have diversifiable keys.
		Assert.True(new ZcashAccount(UnifiedViewingKey.Incoming.Create(this.DefaultAccount.IncomingViewing.Orchard!)).HasDiversifiableKeys);

		// Sapling only accounts have diversifiable keys.
		Assert.True(new ZcashAccount(UnifiedViewingKey.Incoming.Create(this.DefaultAccount.IncomingViewing.Sapling!)).HasDiversifiableKeys);

		// Transparent only accounts have no diversifiable keys.
		Assert.False(new ZcashAccount(UnifiedViewingKey.Incoming.Create(this.DefaultAccount.IncomingViewing.Transparent!)).HasDiversifiableKeys);
	}

	[Fact]
	public void TryImportAccount_InvalidKey()
	{
		Assert.False(ZcashAccount.TryImportAccount("abc", out ZcashAccount? account));
		Assert.Null(account);
	}

	[Fact]
	public void TryImportAccount_Spending_Orchard()
	{
		ZcashAccount account = this.ImportAccount(this.DefaultAccount.Spending!.Orchard!.TextEncoding);
		Assert.NotNull(account);

		Assert.NotNull(account.Spending);
		Assert.NotNull(account.Spending.Orchard);
		Assert.Null(account.Spending.Sapling);
		Assert.Null(account.Spending.Transparent);

		Assert.NotNull(account.FullViewing);
		Assert.NotNull(account.FullViewing.Orchard);

		Assert.NotNull(account.IncomingViewing.Orchard);
	}

	[Fact]
	public void TryImportAccount_Spending_Sapling()
	{
		ZcashAccount account = this.ImportAccount(this.DefaultAccount.Spending!.Sapling!.TextEncoding);
		Assert.NotNull(account);

		Assert.NotNull(account.Spending);
		Assert.Null(account.Spending.Orchard);
		Assert.NotNull(account.Spending.Sapling);
		Assert.Null(account.Spending.Transparent);

		Assert.NotNull(account.FullViewing);
		Assert.NotNull(account.FullViewing.Sapling);

		Assert.NotNull(account.IncomingViewing.Sapling);
	}

	[Fact]
	public void TryImportAccount_UVK()
	{
		ZcashAccount account = this.ImportAccount(this.DefaultAccount.FullViewing!.UnifiedKey.TextEncoding);
		Assert.NotNull(account);

		Assert.Null(account.Spending);

		Assert.NotNull(account.FullViewing);
		Assert.NotNull(account.FullViewing.Transparent);
		Assert.NotNull(account.FullViewing.Sapling);
		Assert.NotNull(account.FullViewing.Orchard);

		Assert.NotNull(account.IncomingViewing);
		Assert.NotNull(account.IncomingViewing.Transparent);
		Assert.NotNull(account.IncomingViewing.Sapling);
		Assert.NotNull(account.IncomingViewing.Orchard);
	}

	[Fact]
	public void TryImportAccount_FullViewing_Orchard()
	{
		ZcashAccount account = this.ImportAccount(this.DefaultAccount.FullViewing!.Orchard!.TextEncoding);
		Assert.NotNull(account);

		Assert.Null(account.Spending);

		Assert.NotNull(account.FullViewing);
		Assert.NotNull(account.FullViewing.Orchard);
		Assert.Null(account.FullViewing.Transparent);
		Assert.Null(account.FullViewing.Sapling);

		Assert.NotNull(account.IncomingViewing);
		Assert.NotNull(account.IncomingViewing.Orchard);
		Assert.Null(account.IncomingViewing.Transparent);
		Assert.Null(account.IncomingViewing.Sapling);
	}

	[Fact]
	public void TryImportAccount_ExtendedFullViewing_Sapling()
	{
		ZcashAccount account = this.ImportAccount(Zip32.CreateSaplingAccount(0).ExtendedFullViewingKey.TextEncoding);
		Assert.NotNull(account);

		Assert.Null(account.Spending);

		Assert.NotNull(account.FullViewing);
		Assert.Null(account.FullViewing.Orchard);
		Assert.NotNull(account.FullViewing.Sapling);
		Assert.Null(account.FullViewing.Transparent);

		Assert.NotNull(account.IncomingViewing);
		Assert.Null(account.IncomingViewing.Orchard);
		Assert.NotNull(account.IncomingViewing.Sapling);
		Assert.Null(account.IncomingViewing.Transparent);
	}

	[Fact]
	public void TryImportAccount_Spending_Transparent()
	{
		ZcashAccount account = this.ImportAccount(this.DefaultAccount.Spending!.Transparent!.TextEncoding);
		Assert.NotNull(account);

		Assert.NotNull(account.Spending);
		Assert.Null(account.Spending.Orchard);
		Assert.Null(account.Spending.Sapling);
		Assert.NotNull(account.Spending.Transparent);

		Assert.NotNull(account.FullViewing);
		Assert.Null(account.FullViewing.Orchard);
		Assert.Null(account.FullViewing.Sapling);
		Assert.NotNull(account.FullViewing.Transparent);

		Assert.NotNull(account.IncomingViewing);
		Assert.Null(account.IncomingViewing.Orchard);
		Assert.Null(account.IncomingViewing.Sapling);
		Assert.NotNull(account.IncomingViewing.Transparent);
	}

	[Fact]
	public void TryImportAccount_FullViewing_Transparent()
	{
		ZcashAccount account = this.ImportAccount(this.DefaultAccount.FullViewing!.Transparent!.TextEncoding);
		Assert.NotNull(account);

		Assert.Null(account.Spending);

		Assert.NotNull(account.FullViewing);
		Assert.Null(account.FullViewing.Orchard);
		Assert.Null(account.FullViewing.Sapling);
		Assert.NotNull(account.FullViewing.Transparent);

		Assert.NotNull(account.IncomingViewing);
		Assert.Null(account.IncomingViewing.Orchard);
		Assert.Null(account.IncomingViewing.Sapling);
		Assert.NotNull(account.IncomingViewing.Transparent);
	}

	[Fact]
	public void TryImportAccount_IncomingViewing_Orchard()
	{
		ZcashAccount account = this.ImportAccount(this.DefaultAccount.IncomingViewing.Orchard!.TextEncoding);
		Assert.NotNull(account);

		Assert.Null(account.Spending);

		Assert.Null(account.FullViewing);

		Assert.NotNull(account.IncomingViewing);
		Assert.NotNull(account.IncomingViewing.Orchard);
		Assert.Null(account.IncomingViewing.Sapling);
		Assert.Null(account.IncomingViewing.Transparent);
	}

	private ZcashAccount ImportAccount(string encoded)
	{
		this.logger.WriteLine(encoded);
		Assert.True(ZcashAccount.TryImportAccount(encoded, out ZcashAccount? account));
		return account;
	}
}
