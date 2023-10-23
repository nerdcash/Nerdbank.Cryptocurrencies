// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Zcash.Sapling;

namespace Sapling;

public class FullViewingKeyTests : TestBase
{
	private readonly ITestOutputHelper logger;
	private readonly FullViewingKey fvk = new Zip32HDWallet(Mnemonic, ZcashNetwork.MainNet).CreateSaplingAccount().FullViewingKey;

	public FullViewingKeyTests(ITestOutputHelper logger)
	{
		this.logger = logger;
	}

	[Theory, PairwiseData]
	public void TextEncoding_TryDecode(bool testNet)
	{
		ZcashNetwork network = testNet ? ZcashNetwork.TestNet : ZcashNetwork.MainNet;
		string expected = testNet
			? "zviewtestsapling16ptegdee2jewf4sv05ykk3nd8yvhpjvm234k5ct0zxw8xvxueurr9zr0h928thlde6mdckgterxa5d6fz4r79hkgsppq3j2tek0hcvhtjjaxnk8vlkuw0e0k6j555085ygpnvm4q9ku26xapl3eyugjalsjzwdwq"
			: "zviews1hjqajk4hheqry6ye4ygz6jysrh2ekzcyvr5375a9v8vdwz8jt6xz2f0xwjlnsrkd6sra7fl7efjk0lvd3q0mm62n4kkfq0q4laqtygahrnketg356x8q6gjyczcf4wmyhs3mgwt3hktwfre8002u5aunvu6d0rtr";
		Zip32HDWallet wallet = new(Mnemonic, network);
		Zip32HDWallet.Sapling.ExtendedSpendingKey account = wallet.CreateSaplingAccount(0);
		string actual = account.FullViewingKey.WithoutDiversifier.TextEncoding;
		this.logger.WriteLine(actual);
		Assert.Equal(expected, actual);

		Assert.True(FullViewingKey.TryDecode(actual, out _, out _, out FullViewingKey? decoded));
		Assert.Equal(account.FullViewingKey.WithoutDiversifier, decoded);
	}

	[Fact]
	public void TryDecode()
	{
		Assert.True(FullViewingKey.TryDecode(this.fvk.TextEncoding, out DecodeError? decodeError, out string? errorMessage, out FullViewingKey? imported));
		Assert.Null(decodeError);
		Assert.Null(errorMessage);
		Assert.NotNull(imported);
		Assert.Equal(this.fvk.TextEncoding, imported.TextEncoding);
	}

	[Fact]
	public void TryDecode_ViaInterface()
	{
		Assert.True(TryDecodeViaInterface<FullViewingKey>(this.fvk.TextEncoding, out DecodeError? decodeError, out string? errorMessage, out IKeyWithTextEncoding? imported));
		Assert.Null(decodeError);
		Assert.Null(errorMessage);
		Assert.NotNull(imported);
		Assert.Equal(this.fvk.TextEncoding, imported.TextEncoding);
	}

	[Fact]
	public void TryDecode_Fail()
	{
		Assert.False(FullViewingKey.TryDecode("fail", out DecodeError? decodeError, out string? errorMessage, out FullViewingKey? imported));
		Assert.NotNull(decodeError);
		Assert.NotNull(errorMessage);
		Assert.Null(imported);
	}

	[Fact]
	public void TryDecode_ViaInterface_Fail()
	{
		Assert.False(TryDecodeViaInterface<FullViewingKey>("fail", out DecodeError? decodeError, out string? errorMessage, out IKeyWithTextEncoding? imported));
		Assert.NotNull(decodeError);
		Assert.NotNull(errorMessage);
		Assert.Null(imported);
	}
}
