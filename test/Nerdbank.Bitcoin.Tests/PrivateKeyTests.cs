// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using NBitcoin.Secp256k1;

public class PrivateKeyTests
{
	private readonly ITestOutputHelper logger;

	public PrivateKeyTests(ITestOutputHelper logger)
	{
		this.logger = logger;
	}

	[Theory, PairwiseData]
	public void Ctor(bool isTestNet)
	{
		PrivateKey bitcoinPrivateKey = CreatePrivateKey(isTestNet);
		Assert.Equal(isTestNet, bitcoinPrivateKey.IsTestNet);
	}

	[Theory, PairwiseData]
	public void EncodeDecode(bool isTestNet)
	{
		PrivateKey bitcoinPrivateKey = CreatePrivateKey(isTestNet);
		this.logger.WriteLine(bitcoinPrivateKey.TextEncoding);

		Assert.True(PrivateKey.TryDecode(bitcoinPrivateKey.TextEncoding, out _, out _, out PrivateKey? decodedKey));
		Assert.Equal(bitcoinPrivateKey.CryptographicKey.sec.ToBytes(), decodedKey.CryptographicKey.sec.ToBytes());
		Assert.Equal(bitcoinPrivateKey.IsTestNet, decodedKey.IsTestNet);
	}

	[Theory, PairwiseData]
	public void PublicKey(bool isTestNet)
	{
		PrivateKey bitcoinPrivateKey = CreatePrivateKey(isTestNet);
		Assert.Equal(isTestNet, bitcoinPrivateKey.PublicKey.IsTestNet);
	}

	private static PrivateKey CreatePrivateKey(bool isTestNet)
	{
		Span<byte> keyBytes = stackalloc byte[32];
		RandomNumberGenerator.Fill(keyBytes);
		ECPrivKey privKey = ECPrivKey.Create(keyBytes);
		return new(privKey, isTestNet);
	}
}
