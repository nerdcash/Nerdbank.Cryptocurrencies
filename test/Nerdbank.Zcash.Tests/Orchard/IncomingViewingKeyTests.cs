// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Numerics;
using Nerdbank.Zcash.Orchard;

namespace Orchard;

public class IncomingViewingKeyTests : TestBase
{
	private readonly IncomingViewingKey ivk = new Zip32HDWallet(Mnemonic, ZcashNetwork.MainNet).CreateOrchardAccount().IncomingViewingKey;

	[Fact]
	public void TryGetDiversifierIndex_And_CheckReceiver()
	{
		Zip32HDWallet wallet = new(Mnemonic, ZcashNetwork.TestNet);
		Zip32HDWallet.Orchard.ExtendedSpendingKey account1 = wallet.CreateOrchardAccount(0);
		Zip32HDWallet.Orchard.ExtendedSpendingKey account2 = wallet.CreateOrchardAccount(1);
		OrchardReceiver receiver = account1.IncomingViewingKey.CreateReceiver(3);

		Assert.True(account1.IncomingViewingKey.CheckReceiver(receiver));
		Assert.True(account1.IncomingViewingKey.TryGetDiversifierIndex(receiver, out DiversifierIndex? idx));
		Assert.Equal(3, idx.Value.ToBigInteger());

		Assert.False(account2.IncomingViewingKey.CheckReceiver(receiver));
		Assert.False(account2.IncomingViewingKey.TryGetDiversifierIndex(receiver, out idx));
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
