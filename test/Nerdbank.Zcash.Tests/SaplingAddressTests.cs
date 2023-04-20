// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public class SaplingAddressTests : TestBase
{
    [Fact]
    public void Network()
    {
        Assert.Equal(ZcashNetwork.MainNet, Assert.IsType<SaplingAddress>(ZcashAddress.Parse(ValidSaplingAddress)).Network);
    }
}
