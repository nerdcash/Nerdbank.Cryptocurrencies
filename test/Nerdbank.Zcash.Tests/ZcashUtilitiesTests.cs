// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Cryptocurrencies.Exchanges;

public class ZcashUtilitiesTests : TestBase
{
	private readonly ITestOutputHelper logger;

	public ZcashUtilitiesTests(ITestOutputHelper logger)
	{
		this.logger = logger;
	}

	[Fact]
	public void GetTickerName()
	{
		Assert.Equal("ZEC", ZcashUtilities.GetTickerName(ZcashNetwork.MainNet));
		Assert.Equal("TAZ", ZcashUtilities.GetTickerName(ZcashNetwork.TestNet));
	}

	[Fact]
	public void AsSecurity_MainNet()
	{
		Assert.Equal(Security.ZEC, ZcashUtilities.AsSecurity(ZcashNetwork.MainNet));
	}

	[Fact]
	public void AsSecurity_TestNet()
	{
		Security actual = ZcashUtilities.AsSecurity(ZcashNetwork.TestNet);
		Assert.Equal("TAZ", actual.TickerSymbol);
		Assert.Equal(8, actual.Precision);
		Assert.Equal("Zcash (testnet)", actual.Name);
	}

	[Theory, PairwiseData]
	public void TryParseKey_UnifiedFullViewingKey(ZcashNetwork network)
	{
		this.Assert_TryParseKey(network, a => a.FullViewing!.UnifiedKey);
	}

	[Theory, PairwiseData]
	public void TryParseKey_UnifiedIncomingViewingKey(ZcashNetwork network)
	{
		this.Assert_TryParseKey(network, a => a.IncomingViewing.UnifiedKey);
	}

	[Theory, PairwiseData]
	public void TryParseKey_Orchard_SpendingKey(ZcashNetwork network)
	{
		this.Assert_TryParseKey(network, a => a.Spending!.Orchard!);
	}

	[Theory, PairwiseData]
	public void TryParseKey_Orchard_FullViewingKey(ZcashNetwork network)
	{
		this.Assert_TryParseUnifiedKey(network, a => a.FullViewing!.Orchard!);
	}

	[Theory, PairwiseData]
	public void TryParseKey_Orchard_IncomingViewingKey(ZcashNetwork network)
	{
		this.Assert_TryParseUnifiedKey(network, a => a.IncomingViewing.Orchard!);
	}

	[Theory, PairwiseData]
	public void TryParseKey_Sapling_SpendingKey(ZcashNetwork network)
	{
		this.Assert_TryParseKey(network, a => a.Spending!.Sapling!);
	}

	[Theory, PairwiseData]
	public void TryParseKey_Sapling_ExtendedFullViewingKey(ZcashNetwork network)
	{
		// We can't round-trip a ZcashAccount.FullViewing.Sapling key because
		// a Sapling DiversifiableFullViewingKey has no defined text encoding.
		// Only FVKs (which lack the diversifier key) and extended fvk's have
		// defined text encodings.
		Zip32HDWallet.Sapling.ExtendedSpendingKey saplingESK = Zip32HDWallet.Sapling.Create(Bip39Mnemonic.Create(Zip32HDWallet.MinimumEntropyLengthInBits), network);
		Zip32HDWallet.Sapling.ExtendedFullViewingKey saplingEFVK = saplingESK.ExtendedFullViewingKey;
		this.Assert_KeyRoundTrip(saplingEFVK);
	}

	[Theory, PairwiseData]
	public void TryParseKey_Sapling_FullViewingKey(ZcashNetwork network)
	{
		// We specifically want to test round-tripping of the non-diversifiable key here.
		this.Assert_TryParseKey<Nerdbank.Zcash.Sapling.FullViewingKey>(network, a => a.FullViewing!.Sapling!);
	}

	[Theory, PairwiseData]
	public void TryParseKey_Sapling_IncomingViewingKey(ZcashNetwork network)
	{
		// We specifically want to test round-tripping of the non-diversifiable key here.
		this.Assert_TryParseKey<Nerdbank.Zcash.Sapling.IncomingViewingKey>(network, a => a.IncomingViewing.Sapling!);
	}

	[Fact]
	public void TryParseKey_InvalidKey()
	{
		Assert.False(ZcashUtilities.TryParseKey("abc", out IKeyWithTextEncoding? key));
		Assert.Null(key);
	}

	[Theory, PairwiseData]
	public void TryParseKey_Transparent_SpendingKey(ZcashNetwork network)
	{
		this.Assert_TryParseKey(network, a => a.Spending!.Transparent!);
	}

	[Theory, PairwiseData]
	public void TryParseKey_Transparent_FullViewingKey(ZcashNetwork network)
	{
		this.Assert_TryParseKey(network, a => a.FullViewing!.Transparent!);
	}

	private void Assert_TryParseKey<TKey>(ZcashNetwork network, Func<ZcashAccount, TKey> keyExtractor)
		where TKey : class, IKeyWithTextEncoding
	{
		ZcashAccount account = new(new Zip32HDWallet(Bip39Mnemonic.Create(Zip32HDWallet.MinimumEntropyLengthInBits), network));

		TKey original = keyExtractor(account);
		this.Assert_KeyRoundTrip(original);
	}

	private void Assert_KeyRoundTrip<TKey>(TKey original)
		where TKey : class, IKeyWithTextEncoding
	{
		string encoded = original.TextEncoding;
		this.logger.WriteLine(encoded);
		Assert.True(ZcashUtilities.TryParseKey(encoded, out IKeyWithTextEncoding? parsed));
		Assert.IsType<TKey>(parsed);
		Assert.Equal(encoded, parsed.TextEncoding);
	}

	private void Assert_TryParseUnifiedKey<TKey>(ZcashNetwork network, Func<ZcashAccount, TKey> keyExtractor)
		where TKey : class, IKeyWithTextEncoding, IIncomingViewingKey
	{
		ZcashAccount account = new(new Zip32HDWallet(Bip39Mnemonic.Create(Zip32HDWallet.MinimumEntropyLengthInBits), network));

		TKey original = keyExtractor(account);
		string encoded = original.TextEncoding;
		this.logger.WriteLine(encoded);
		Assert.True(ZcashUtilities.TryParseKey(encoded, out IKeyWithTextEncoding? parsed));
		UnifiedViewingKey uvk = Assert.IsAssignableFrom<UnifiedViewingKey>(parsed);
		TKey? parsedKey = uvk.GetViewingKey<TKey>();
		Assert.NotNull(parsedKey);
		Assert.Equal(encoded, parsedKey.TextEncoding);
	}
}
