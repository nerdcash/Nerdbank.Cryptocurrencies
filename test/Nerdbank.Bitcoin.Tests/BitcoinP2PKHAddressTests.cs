// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using NBitcoin.Secp256k1;

public class BitcoinP2PKHAddressTests
{
	private readonly ITestOutputHelper logger;

	public BitcoinP2PKHAddressTests(ITestOutputHelper logger)
	{
		this.logger = logger;
	}

	[Theory]
	[InlineData("031e7bcc70c72770dbb72fea022e8a6d07f814d2ebe4de9ae3f7af75bf706902a7", false, "17JsmEygbbEUEpvt4PFtYaTeSqfb9ki1F1")]
	[InlineData("031e7bcc70c72770dbb72fea022e8a6d07f814d2ebe4de9ae3f7af75bf706902a7", true, "mmpq4J4fQcfj1wQVmxEGNVfyJqGJ8gjMjQ")]
	public void AddressConstruction(string hexEncodedPublicKey, bool isTestNet, string expectedAddress)
	{
		ECPubKey pubKey = ECPubKey.Create(Convert.FromHexString(hexEncodedPublicKey));
		BitcoinP2PKHAddress addr = new(pubKey, isTestNet);
		this.logger.WriteLine(addr.TextEncoding);
		Assert.Equal(expectedAddress, addr.TextEncoding);
	}

	[Theory]
	[InlineData("17JsmEygbbEUEpvt4PFtYaTeSqfb9ki1F1", false, "453233600A96384BB8D73D400984117AC84D7E8B")]
	[InlineData("mmpq4J4fQcfj1wQVmxEGNVfyJqGJ8gjMjQ", true, "453233600A96384BB8D73D400984117AC84D7E8B")]
	public void TryDecode(string address, bool isTestNet, string publicKeyHashHex)
	{
		Assert.True(BitcoinP2PKHAddress.TryDecode(address, out _, out _, out BitcoinP2PKHAddress? bitcoinAddress));
		Assert.Equal(address, bitcoinAddress.TextEncoding);
		Assert.Equal(isTestNet, bitcoinAddress.IsTestNet);
		Assert.Equal(Convert.FromHexString(publicKeyHashHex), bitcoinAddress.PublicKeyHash.ToArray());
	}
}
