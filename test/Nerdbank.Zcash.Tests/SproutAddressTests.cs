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
        Assert.Equal(ZcashNetwork.MainNet, Assert.IsType<SproutAddress>(ZcashAddress.Parse(ValidSproutAddress)).Network);
    }

    [Theory, MemberData(nameof(InvalidAddresses))]
    public void TryParse_Invalid(string address)
    {
        Assert.False(ZcashAddress.TryParse(address, out _));
    }
}
