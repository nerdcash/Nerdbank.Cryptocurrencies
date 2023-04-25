// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public class SproutAddressTests : TestBase
{
	public static object?[][] InvalidAddresses => new object?[][]
	{
		new object?[] { "zt" },
		new object?[] { "ztoeuchch" },
		new object?[] { "zc" },
		new object?[] { "zceuoch" },
	};

	[Fact]
	public void Network()
	{
		Assert.Equal(ZcashNetwork.MainNet, Assert.IsType<SproutAddress>(ZcashAddress.Parse(ValidSproutAddress)).Network);
	}

	[Theory, MemberData(nameof(InvalidAddresses))]
	public void TryParse_Invalid(string address)
	{
		Assert.False(ZcashAddress.TryParse(address, out _));
	}

	[Fact]
	public void Ctor_Receiver()
	{
		SproutReceiver? receiver = ZcashAddress.Parse(ValidSproutAddress).GetPoolReceiver<SproutReceiver>();
		Assert.NotNull(receiver);
		SproutAddress addr = new(receiver.Value);
		Assert.Equal(receiver, addr.GetPoolReceiver<SproutReceiver>());
		Assert.Equal("zc8E5gYid86n4bo2Usdq1cpr7PpfoJGzttwBHEEgGhGkLUg7SPPVFNB2AkRFXZ7usfphup5426dt1buMmY3fkYeRrQGLa8y", addr.Address);
	}
}
