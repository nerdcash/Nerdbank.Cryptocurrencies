// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft;

public class TransparentAddressTests : TestBase
{
	private static readonly TransparentAddress ParsedP2PKHAddress = (TransparentP2PKHAddress)ZcashAddress.Parse(ValidTransparentP2PKHAddress);

	private readonly ITestOutputHelper logger;

	public TransparentAddressTests(ITestOutputHelper logger)
	{
		this.logger = logger;
	}

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

	[Fact]
	public void TryParse_BadNetwork()
	{
		// Manufacture a transparent address with a bad network header.
		byte[] receiver = new byte[22];
		receiver[0] = 0x1c;
		receiver[1] = 0xbb;
		char[] addrChars = new char[50];
		int count = Base58Check.Encode(receiver, addrChars);
		string addr = new(addrChars, 0, count);
		Assumes.True(addr.StartsWith('t'));

		Assert.False(ZcashAddress.TryParse(addr, out _));
	}

	[Theory]
	[InlineData("tdasgh2344235")]
	[InlineData("tadadadadadadadadadadadadadadadadadadadadadadadadadadadadadadadadadadadadadadadada")]
	[InlineData("tbalsdhfldsfhsdhfgdfgdf")]
	public void TryParse_FuzzInputs_ShouldReturnFalse(string input)
	{
		Assert.False(ZcashAddress.TryParse(input, out ZcashAddress? address, out ParseError? errorCode, out string? errorMessage));
		this.logger.WriteLine(errorMessage);

		Assert.Null(address);
		Assert.NotNull(errorCode);
		Assert.NotNull(errorMessage);
	}
}
