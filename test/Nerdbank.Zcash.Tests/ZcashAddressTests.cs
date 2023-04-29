// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public class ZcashAddressTests : TestBase
{
	public static object[][] ValidAddresses => new object[][]
	{
		new object[] { ValidUnifiedAddressOrchardSapling },
		new object[] { ValidUnifiedAddressOrchard },
		new object[] { ValidSaplingAddress },
		new object[] { ValidSproutAddress },
		new object[] { ValidTransparentP2PKHAddress },
		new object[] { ValidTransparentP2SHAddress },
	};

	public static object?[][] InvalidAddresses => new object?[][]
	{
		new object?[] { string.Empty },
		new object?[] { "foo" },
	};

	[Theory, MemberData(nameof(ValidAddresses))]
	public void Parse_Valid(string address)
	{
		var addr = ZcashAddress.Parse(address);
		Assert.Equal(address, addr.ToString());
	}

	[Theory, MemberData(nameof(ValidAddresses))]
	public void TryParse_Valid(string address)
	{
		Assert.True(ZcashAddress.TryParse(address, out ZcashAddress? addr));
		Assert.Equal(address, addr.ToString());
	}

	[Theory, MemberData(nameof(InvalidAddresses))]
	public void Parse_Invalid(string address)
	{
		Assert.Throws<InvalidAddressException>(() => ZcashAddress.Parse(address));
	}

	[Theory, MemberData(nameof(InvalidAddresses))]
	public void TryParse_Invalid(string address)
	{
		Assert.False(ZcashAddress.TryParse(address, out _));
	}

	[Fact]
	public void Parse_Null()
	{
		Assert.Throws<ArgumentNullException>(() => ZcashAddress.Parse(null!));
	}

	[Fact]
	public void TryParse_Null()
	{
		Assert.Throws<ArgumentNullException>(() => ZcashAddress.TryParse(null!, out _));
	}

	[Theory]
	[InlineData(ValidUnifiedAddressOrchard, typeof(OrchardAddress))]
	[InlineData(ValidUnifiedAddressOrchardSapling, typeof(UnifiedAddress))]
	[InlineData(ValidUnifiedAddressSapling, typeof(UnifiedAddress))]
	[InlineData(ValidSaplingAddress, typeof(SaplingAddress))]
	[InlineData(ValidSproutAddress, typeof(SproutAddress))]
	[InlineData(ValidTransparentP2PKHAddress, typeof(TransparentP2PKHAddress))]
	[InlineData(ValidTransparentP2SHAddress, typeof(TransparentP2SHAddress))]
	public void Parse_ReturnsAppropriateType(string address, Type expectedKind)
	{
		var addr = ZcashAddress.Parse(address);
		Assert.IsAssignableFrom(expectedKind, addr);
	}

	[Fact]
	public void ImplicitlyCastableToString()
	{
		var addr = ZcashAddress.Parse(ValidTransparentP2PKHAddress);
		string str = addr.Address;
		Assert.Equal(ValidTransparentP2PKHAddress, str);
	}

	[Fact]
	public void Equality()
	{
		var addr1a = ZcashAddress.Parse(ValidTransparentP2PKHAddress);
		var addr1b = ZcashAddress.Parse(ValidTransparentP2PKHAddress);
		var addr2 = ZcashAddress.Parse(ValidTransparentP2SHAddress);
		Assert.Equal(addr1a, addr1b);
		Assert.NotEqual(addr1a, addr2);
	}

	[Fact]
	public void HashCodes()
	{
		var addr1a = ZcashAddress.Parse(ValidTransparentP2PKHAddress);
		var addr1b = ZcashAddress.Parse(ValidTransparentP2PKHAddress);
		var addr2 = ZcashAddress.Parse(ValidTransparentP2SHAddress);
		Assert.Equal(addr1a.GetHashCode(), addr1b.GetHashCode());
		Assert.NotEqual(addr1a.GetHashCode(), addr2.GetHashCode());
	}

	[Fact]
	public void ImplicitCast()
	{
		string address = ZcashAddress.Parse(ValidTransparentP2SHAddress);
		Assert.Equal(ValidTransparentP2SHAddress, address);
	}

	[Fact]
	public void ImplicitCast_Null()
	{
		string? address = (ZcashAddress?)null;
		Assert.Null(address);
	}
}
