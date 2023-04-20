// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public class ZcashAddressTests
{
    private const string ValidUnifiedAddressOrchardSaplingTransparent = "u1vv2ws6xhs72faugmlrasyeq298l05rrj6wfw8hr3r29y3czev5qt4ugp7kylz6suu04363ze92dfg8ftxf3237js0x9p5r82fgy47xkjnw75tqaevhfh0rnua72hurt22v3w3f7h8yt6mxaa0wpeeh9jcm359ww3rl6fj5ylqqv54uuwrs8q4gys9r3cxdm3yslsh3rt6p7wznzhky7";
    private const string ValidUnifiedAddressOrchardSapling = "u10p78pgwpatn9n5zsut79577c78yt59cerl0ymdk7m4ug3hd7cw0fj2c7k20q3ndt2x49zzy69xgl22wr7tgl652lxflaex79xgpg2kyk9m83nzerccpvkxfy47v7xz6g5fqaz3x4tvl6lnkh58j6mj60synt2kr5rgxcpdm3qq9u0nm2";
    private const string ValidUnifiedAddressOrchard = "u1v0j6szgvcquae449dltsrhdhlle4ac8cxd3z8k4j2wtxgfxg6xnq25a900d3yq65mz0l6heqhcj468f7q3l2wnxdsxjrcw90svum7q67";
    private const string ValidSaplingAddress = "zs1andrewyvxpx2d0zthcafwxn0n6clu4rwjhl9fpa86zt0np6pxqxrgar2e2tareutxfxdv2rll5v";
    private const string ValidSproutAddress = "zc***";
    private const string ValidTransparentAddress = "t1a7w3qM23i4ajQcbX5wd6oH4zTY8Bry5vF";

    public ZcashAddressTests()
    {
    }

    [Theory]
    [InlineData(ValidUnifiedAddressOrchardSapling, true)]
    [InlineData(ValidUnifiedAddressOrchard, true)]
    [InlineData(ValidSaplingAddress, true)]
    [InlineData(ValidSproutAddress, true)]
    [InlineData(ValidTransparentAddress, true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("foo", false)]
    public void IsValid(string address, bool expectedValid)
    {
        ZcashAddress addr = new(address);
        Assert.Equal(expectedValid, addr.IsValid);
    }

    [Theory]
    [InlineData(ValidUnifiedAddressOrchard, AddressKind.Unified)]
    [InlineData(ValidUnifiedAddressOrchardSapling, AddressKind.Unified)]
    [InlineData(ValidSaplingAddress, AddressKind.Sapling)]
    [InlineData(ValidSproutAddress, AddressKind.Sprout)]
    [InlineData(ValidTransparentAddress, AddressKind.Transparent)]
    [InlineData("invalid", AddressKind.Invalid)]
    public void Kind(string address, AddressKind expectedKind)
    {
        ZcashAddress addr = new(address);
        Assert.Equal(expectedKind, addr.Kind);
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
        ZcashAddress addr = new(address);
        foreach (Pool pool in Enum.GetValues(typeof(Pool)))
        {
            Assert.Equal(Array.IndexOf(pools, pool) != -1, addr.SupportsPool(pool));
        }
    }

    [Theory]
    [InlineData(ValidUnifiedAddressOrchard)]
    [InlineData(ValidTransparentAddress)]
    [InlineData(ValidSaplingAddress)]
    public void Receivers_Single(string address)
    {
        ZcashAddress addr = new(address);
        Assert.Equal(new[] { addr }, addr.Receivers);
    }

    [Fact]
    public void Receivers_UnifiedMultiple()
    {
        ZcashAddress addr = new(ValidUnifiedAddressOrchardSapling);
        Assert.Equal(
            new[]
            {
                new ZcashAddress("u1v0j6szgvcquae449dltsrhdhlle4ac8cxd3z8k4j2wtxgfxg6xnq25a900d3yq65mz0l6heqhcj468f7q3l2wnxdsxjrcw90svum7q67"),
                new ZcashAddress("zs1znewe2leucm8gsd2ue24kvp3jjjwgrhmytmv0scenaf460kdj70r299a88r8n0pyvwz7c9skfmy"),
            },
            addr.Receivers);

        addr = new(ValidUnifiedAddressOrchardSaplingTransparent);
        Assert.Equal(
            new[]
            {
                new ZcashAddress("u1v0j6szgvcquae449dltsrhdhlle4ac8cxd3z8k4j2wtxgfxg6xnq25a900d3yq65mz0l6heqhcj468f7q3l2wnxdsxjrcw90svum7q67"),
                new ZcashAddress("zs1znewe2leucm8gsd2ue24kvp3jjjwgrhmytmv0scenaf460kdj70r299a88r8n0pyvwz7c9skfmy"),
                new ZcashAddress("t1a7w3qM23i4ajQcbX5wd6oH4zTY8Bry5vF"),
            },
            addr.Receivers);
    }
}
