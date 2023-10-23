// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Zcash.Orchard;

namespace Orchard;

public class FullViewingKeyTests : TestBase
{
	private readonly ITestOutputHelper logger;
	private readonly FullViewingKey fvk = new Zip32HDWallet(Mnemonic, ZcashNetwork.MainNet).CreateOrchardAccount().FullViewingKey;

	public FullViewingKeyTests(ITestOutputHelper logger)
	{
		this.logger = logger;
	}

	[Fact]
	public void DeriveInternal()
	{
		this.logger.WriteLine($"Public address: {this.fvk.IncomingViewingKey.DefaultAddress}");
		FullViewingKey internalFvk = this.fvk.DeriveInternal();
		this.logger.WriteLine($"Internal address: {internalFvk.IncomingViewingKey.DefaultAddress}");
		Assert.Equal("u16ddqv7tagn5wvfwwkarzq3wlw2kvpqldsd6dzy8f9lypm2lc9qxt04jurykft0kl69hg57j39866fde7sz0effua2sh67ld5gg7xlrrr", internalFvk.IncomingViewingKey.DefaultAddress);
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
