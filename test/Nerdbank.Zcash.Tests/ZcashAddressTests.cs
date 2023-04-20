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
        new object[] { ValidTransparentAddress },
    };

    public static object?[][] InvalidAddresses => new object?[][]
    {
        new object?[] { null },
        new object?[] { string.Empty },
        new object?[] { "foo" },
    };

    [Theory, MemberData(nameof(ValidAddresses))]
    public void Parse_Valid(string address)
    {
        ZcashAddress addr = ZcashAddress.Parse(address);
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
        Assert.Throws<ArgumentException>(() => ZcashAddress.Parse(address));
    }

    [Theory, MemberData(nameof(InvalidAddresses))]
    public void TryParse_Invalid(string address)
    {
        Assert.False(ZcashAddress.TryParse(address, out _));
    }

    [Theory]
    [InlineData(ValidUnifiedAddressOrchard, typeof(UnifiedAddress))]
    [InlineData(ValidUnifiedAddressOrchardSapling, typeof(UnifiedAddress))]
    [InlineData(ValidSaplingAddress, typeof(SaplingAddress))]
    [InlineData(ValidSproutAddress, typeof(SproutAddress))]
    [InlineData(ValidTransparentAddress, typeof(TransparentAddress))]
    public void Parse_ReturnsAppropriateType(string address, Type expectedKind)
    {
        ZcashAddress addr = ZcashAddress.Parse(address);
        Assert.IsType(expectedKind, addr);
    }

    [Theory]
    [InlineData(ValidUnifiedAddressOrchard, Pool.Orchard)]
    [InlineData(ValidUnifiedAddressOrchardSapling, Pool.Orchard, Pool.Sapling)]
    [InlineData(ValidUnifiedAddressOrchardSaplingTransparent, Pool.Orchard, Pool.Sapling, Pool.Transparent)]
    [InlineData(ValidSaplingAddress, Pool.Sapling)]
    [InlineData(ValidSproutAddress, Pool.Sprout)]
    [InlineData(ValidTransparentAddress, Pool.Transparent)]
    public void SupportsPool(string address, params Pool[] pools)
    {
        ZcashAddress addr = ZcashAddress.Parse(address);
        foreach (Pool pool in Enum.GetValues(typeof(Pool)))
        {
            Assert.Equal(Array.IndexOf(pools, pool) != -1, addr.SupportsPool(pool));
        }
    }

    [Fact]
    public void ImplicitlyCastableToString()
    {
        ZcashAddress addr = ZcashAddress.Parse(ValidTransparentAddress);
        string str = addr;
        Assert.Equal(ValidTransparentAddress, str);
    }
}
