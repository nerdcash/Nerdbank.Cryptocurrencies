// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public class TransparentAddressTests : TestBase
{
	private static readonly TransparentAddress ParsedP2PKHAddress = (TransparentP2PKHAddress)ZcashAddress.Parse(ValidTransparentP2PKHAddress);

	public static object?[][] InvalidAddresses => new object?[][]
	{
		new object?[] { "T" },
		new object?[] { "T3KQYMMqMBTv8254UqwmaLzW5NDT879KzK8" },
		new object?[] { "t1a7w3qM23i4ajQcbX5wd6oH4zTY8Bry5vFz" },
	};

	[Fact]
	public void Network()
	{
		Assert.Equal(ZcashNetwork.MainNet, ParsedP2PKHAddress.Network);
		Assert.Equal(ZcashNetwork.MainNet, Assert.IsAssignableFrom<TransparentAddress>(ZcashAddress.Parse(ValidTransparentP2SHAddress)).Network);
	}

	[Theory, MemberData(nameof(InvalidAddresses))]
	public void TryParse_Invalid(string address)
	{
		Assert.False(ZcashAddress.TryParse(address, out _));
	}
}
