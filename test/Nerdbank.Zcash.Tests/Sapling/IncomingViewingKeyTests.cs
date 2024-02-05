// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Numerics;
using Nerdbank.Zcash.Sapling;

namespace Sapling;

public class IncomingViewingKeyTests : TestBase
{
	private readonly ITestOutputHelper logger;
	private readonly IncomingViewingKey ivk = new Zip32HDWallet(Mnemonic, ZcashNetwork.MainNet).CreateSaplingAccount().IncomingViewingKey;

	public IncomingViewingKeyTests(ITestOutputHelper logger)
	{
		this.logger = logger;
	}

	[Theory, PairwiseData]
	public void TextEncoding_TryDecode(bool testNet)
	{
		ZcashNetwork network = testNet ? ZcashNetwork.TestNet : ZcashNetwork.MainNet;
		string expected = testNet
			? "zivktestsapling1j9ne49vsmdgp9wrysf4drdu2fjqc8sue7fydej82tfz9m9fl5yzs92j0h3"
			: "zivks1dj9c724gxstqma92fywr3upl2acyu6jg380ml4dlfau8akcuv5rq8xhc3n";
		Zip32HDWallet wallet = new(Mnemonic, network);
		Zip32HDWallet.Sapling.ExtendedSpendingKey account = wallet.CreateSaplingAccount(0);
		string actual = account.FullViewingKey.IncomingViewingKey.WithoutDiversifierKey.TextEncoding;
		this.logger.WriteLine(actual);
		Assert.Equal(expected, actual);

		Assert.True(IncomingViewingKey.TryDecode(actual, out DecodeError? decodeError, out string? errorMessage, out IncomingViewingKey? key));
		Assert.Null(decodeError);
		Assert.Null(errorMessage);
		Assert.Equal(account.FullViewingKey.IncomingViewingKey.WithoutDiversifierKey, key);
	}

	[Fact]
	public void TryGetDiversifierIndex_And_CheckReceiver()
	{
		Zip32HDWallet wallet = new(Mnemonic, ZcashNetwork.TestNet);
		Zip32HDWallet.Sapling.ExtendedSpendingKey account1 = wallet.CreateSaplingAccount(0);
		Zip32HDWallet.Sapling.ExtendedSpendingKey account2 = wallet.CreateSaplingAccount(1);
		DiversifierIndex index = 3;
		Assert.True(account1.IncomingViewingKey.TryCreateReceiver(ref index, out SaplingReceiver? receiver));

		Assert.True(account1.IncomingViewingKey.CheckReceiver(receiver.Value));
		Assert.True(account1.IncomingViewingKey.TryGetDiversifierIndex(receiver.Value, out DiversifierIndex? idx));
		Assert.Equal(index, idx);

		Assert.False(account2.IncomingViewingKey.CheckReceiver(receiver.Value));
		Assert.False(account2.IncomingViewingKey.TryGetDiversifierIndex(receiver.Value, out idx));
		Assert.Null(idx);
	}

	[Fact]
	public void TryDecode()
	{
		Assert.True(IncomingViewingKey.TryDecode(this.ivk.TextEncoding, out DecodeError? decodeError, out string? errorMessage, out IncomingViewingKey? imported));
		Assert.Null(decodeError);
		Assert.Null(errorMessage);
		Assert.NotNull(imported);
		Assert.Equal(this.ivk.TextEncoding, imported.TextEncoding);
	}

	[Fact]
	public void TryDecode_ViaInterface()
	{
		Assert.True(TryDecodeViaInterface<IncomingViewingKey>(this.ivk.TextEncoding, out DecodeError? decodeError, out string? errorMessage, out IKeyWithTextEncoding? imported));
		Assert.Null(decodeError);
		Assert.Null(errorMessage);
		Assert.NotNull(imported);
		Assert.Equal(this.ivk.TextEncoding, imported.TextEncoding);
	}

	[Fact]
	public void TryDecode_Fail()
	{
		Assert.False(IncomingViewingKey.TryDecode("fail", out DecodeError? decodeError, out string? errorMessage, out IncomingViewingKey? imported));
		Assert.NotNull(decodeError);
		Assert.NotNull(errorMessage);
		Assert.Null(imported);
	}

	[Fact]
	public void TryDecode_ViaInterface_Fail()
	{
		Assert.False(TryDecodeViaInterface<IncomingViewingKey>("fail", out DecodeError? decodeError, out string? errorMessage, out IKeyWithTextEncoding? imported));
		Assert.NotNull(decodeError);
		Assert.NotNull(errorMessage);
		Assert.Null(imported);
	}
}
