// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public class ZcashAddressTests
{
    private const string ValidUnifiedAddress = "u1vv2ws6xhs72faugmlrasyeq298l05rrj6wfw8hr3r29y3czev5qt4ugp7kylz6suu04363ze92dfg8ftxf3237js0x9p5r82fgy47xkjnw75tqaevhfh0rnua72hurt22v3w3f7h8yt6mxaa0wpeeh9jcm359ww3rl6fj5ylqqv54uuwrs8q4gys9r3cxdm3yslsh3rt6p7wznzhky7";
    private const string ValidSaplingAddress = "zs1znewe2leucm8gsd2ue24kvp3jjjwgrhmytmv0scenaf460kdj70r299a88r8n0pyvwz7c9skfmy";
    private const string ValidTransparentAddress = "t1V7d7CJPFJijUPRx7bEQE7CivHirkgWJ8h";

    public ZcashAddressTests()
    {
    }

    [Theory]
    [InlineData(ValidUnifiedAddress, true)]
    [InlineData(ValidSaplingAddress, true)]
    [InlineData(ValidTransparentAddress, true)]
    public void IsValid(string address, bool expectedValid)
    {
        ZcashAddress addr = new(address);
        Assert.Equal(expectedValid, addr.IsValid);
    }

    [Theory]
    [InlineData(ValidUnifiedAddress, AddressKind.Unified)]
    [InlineData(ValidSaplingAddress, AddressKind.Sapling)]
    [InlineData(ValidTransparentAddress, AddressKind.Transparent)]
    public void Kind(string address, AddressKind expectedKind)
    {
        ZcashAddress addr = new(address);
        Assert.Equal(expectedKind, addr.Kind);
    }
}
