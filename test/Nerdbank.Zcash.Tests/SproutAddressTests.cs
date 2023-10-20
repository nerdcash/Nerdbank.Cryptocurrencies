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
		Assert.Equal(ZcashNetwork.MainNet, Assert.IsType<SproutAddress>(ZcashAddress.Decode(ValidSproutAddress)).Network);
	}

	[Fact]
	public void HasShieldedReceiver() => Assert.True(ZcashAddress.Decode(ValidSproutAddress).HasShieldedReceiver);

	[Theory, MemberData(nameof(InvalidAddresses))]
	public void TryDecode_Invalid(string address)
	{
		Assert.False(ZcashAddress.TryDecode(address, out _, out _, out _));
	}

	[Fact]
	public void Ctor_Receiver()
	{
		SproutReceiver? receiver = ZcashAddress.Decode(ValidSproutAddress).GetPoolReceiver<SproutReceiver>();
		Assert.NotNull(receiver);
		SproutAddress addr = new(receiver.Value);
		Assert.Equal(ZcashNetwork.MainNet, addr.Network);
		Assert.Equal(receiver, addr.GetPoolReceiver<SproutReceiver>());
		Assert.Equal("zc8E5gYid86n4bo2Usdq1cpr7PpfoJGzttwBHEEgGhGkLUg7SPPVFNB2AkRFXZ7usfphup5426dt1buMmY3fkYeRrQGLa8y", addr.Address);
	}

	[Fact]
	public void Ctor_Receiver_TestNet()
	{
		SproutReceiver? receiver = ZcashAddress.Decode(ValidSproutAddress).GetPoolReceiver<SproutReceiver>();
		Assert.NotNull(receiver);
		SproutAddress addr = new(receiver.Value, ZcashNetwork.TestNet);
		Assert.Equal(ZcashNetwork.TestNet, addr.Network);
		Assert.Equal(receiver, addr.GetPoolReceiver<SproutReceiver>());
		Assert.Equal("ztJ1EWLKcGwF2S4NA17pAJVdco8Sdkz4AQPxt1cLTEfNuyNswJJc2BbBqYrsRZsp31xbVZwhF7c7a2L9jsF3p3ZwRWpqqyS", addr.Address);
	}
}
