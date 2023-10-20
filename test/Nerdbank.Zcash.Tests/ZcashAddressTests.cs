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
	public void Decode_Valid(string address)
	{
		var addr = ZcashAddress.Decode(address);
		Assert.Equal(address, addr.ToString());
	}

	[Theory, MemberData(nameof(ValidAddresses))]
	public void TryDecode_Valid(string address)
	{
		Assert.True(ZcashAddress.TryDecode(address, out _, out _, out ZcashAddress? addr));
		Assert.Equal(address, addr.ToString());
	}

	[Theory, MemberData(nameof(InvalidAddresses))]
	public void Parse_Invalid(string address)
	{
		Assert.Throws<InvalidAddressException>(() => ZcashAddress.Decode(address));
	}

	[Theory, MemberData(nameof(InvalidAddresses))]
	public void TryDecode_Invalid(string address)
	{
		Assert.False(ZcashAddress.TryDecode(address, out _, out _, out _));
	}

	[Fact]
	public void Parse_Null()
	{
		Assert.Throws<ArgumentNullException>(() => ZcashAddress.Decode(null!));
	}

	[Fact]
	public void TryDecode_Null()
	{
		Assert.Throws<ArgumentNullException>(() => ZcashAddress.TryDecode(null!, out _, out _, out _));
	}

	[Theory]
	[InlineData(ValidUnifiedAddressOrchard, typeof(OrchardAddress))]
	[InlineData(ValidUnifiedAddressOrchardSapling, typeof(UnifiedAddress))]
	[InlineData(ValidUnifiedAddressSapling, typeof(UnifiedAddress))]
	[InlineData(ValidSaplingAddress, typeof(SaplingAddress))]
	[InlineData(ValidSproutAddress, typeof(SproutAddress))]
	[InlineData(ValidTransparentP2PKHAddress, typeof(TransparentP2PKHAddress))]
	[InlineData(ValidTransparentP2SHAddress, typeof(TransparentP2SHAddress))]
	public void Decode_ReturnsAppropriateType(string address, Type expectedKind)
	{
		var addr = ZcashAddress.Decode(address);
		Assert.IsAssignableFrom(expectedKind, addr);
	}

	[Fact]
	public void ImplicitlyCastableToString()
	{
		var addr = ZcashAddress.Decode(ValidTransparentP2PKHAddress);
		string str = addr.Address;
		Assert.Equal(ValidTransparentP2PKHAddress, str);
	}

	[Fact]
	public void Equality()
	{
		var addr1a = ZcashAddress.Decode(ValidTransparentP2PKHAddress);
		var addr1b = ZcashAddress.Decode(ValidTransparentP2PKHAddress);
		var addr2 = ZcashAddress.Decode(ValidTransparentP2SHAddress);
		Assert.Equal(addr1a, addr1b);
		Assert.NotEqual(addr1a, addr2);
	}

	[Fact]
	public void HashCodes()
	{
		var addr1a = ZcashAddress.Decode(ValidTransparentP2PKHAddress);
		var addr1b = ZcashAddress.Decode(ValidTransparentP2PKHAddress);
		var addr2 = ZcashAddress.Decode(ValidTransparentP2SHAddress);
		Assert.Equal(addr1a.GetHashCode(), addr1b.GetHashCode());
		Assert.NotEqual(addr1a.GetHashCode(), addr2.GetHashCode());
	}

	[Fact]
	public void ImplicitCast()
	{
		string address = ZcashAddress.Decode(ValidTransparentP2SHAddress);
		Assert.Equal(ValidTransparentP2SHAddress, address);
	}

	[Fact]
	public void ImplicitCast_Null()
	{
		string? address = (ZcashAddress?)null;
		Assert.Null(address);
	}
}
