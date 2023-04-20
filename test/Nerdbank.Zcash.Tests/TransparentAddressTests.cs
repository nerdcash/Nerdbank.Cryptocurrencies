﻿// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public class TransparentAddressTests : TestBase
{
    [Fact]
    public void Classify()
    {
        Assert.Equal("P2PKH", Assert.IsType<TransparentAddress>(ZcashAddress.Parse(ValidTransparentP2PKHAddress)).Type);
        ////Assert.Equal("P2SH", Assert.IsType<TransparentAddress>(ZcashAddress.Parse(ValidTransparentP2SHAddress)).Type);
    }

    [Fact]
    public void Network()
    {
        Assert.Equal(ZcashNetwork.MainNet, Assert.IsType<TransparentAddress>(ZcashAddress.Parse(ValidTransparentP2PKHAddress)).Network);
        ////Assert.Equal(ZcashNetwork.MainNet, Assert.IsType<TransparentAddress>(ZcashAddress.Parse(ValidTransparentP2SHAddress)).Network);
    }

    [Fact]
    public void ParseThrowsOnInvalidAddress()
    {
        Assert.Throws<FormatException>(() => ZcashAddress.Parse("T3KQYMMqMBTv8254UqwmaLzW5NDT879KzK8"));
    }
}