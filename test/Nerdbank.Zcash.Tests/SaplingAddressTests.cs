// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public class SaplingAddressTests : TestBase
{
	public static object?[][] InvalidAddresses => new object?[][]
	{
		new object?[] { "zs" },
		new object?[] { "zs1znewe2l2ucm8gsd2ue24kvp3jjjwgrhmytmv0scenaf460kdj70r299a88r8n0pyvwz7c9skfmy" },
	};

	[Fact]
	public void Network()
	{
		Assert.Equal(ZcashNetwork.MainNet, Assert.IsType<SaplingAddress>(ZcashAddress.Parse(ValidSaplingAddress)).Network);
	}

	[Theory, MemberData(nameof(InvalidAddresses))]
	public void TryParse_Invalid(string address)
	{
		Assert.False(ZcashAddress.TryParse(address, out _));
	}
}
