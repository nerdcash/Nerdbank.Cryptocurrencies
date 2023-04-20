// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public class ZcashAddressTests : TestBase
{
    [Theory]
    [InlineData(ValidUnifiedAddressOrchardSapling)]
    [InlineData(ValidUnifiedAddressOrchard)]
    [InlineData(ValidSaplingAddress)]
    [InlineData(ValidSproutAddress)]
    [InlineData(ValidTransparentAddress)]
    public void Parse_Valid(string address)
    {
        ZcashAddress addr = ZcashAddress.Parse(address);
        Assert.Equal(address, addr.ToString());
    }

    [Fact]
    public void Parse_Null()
    {
        Assert.Throws<ArgumentNullException>(() => ZcashAddress.Parse(null));
    }

    [Theory]
    [InlineData("")]
    [InlineData("foo")]
    public void Parse_Invalid(string address)
    {
        Assert.Throws<ArgumentException>(() => ZcashAddress.Parse(address));
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
}
