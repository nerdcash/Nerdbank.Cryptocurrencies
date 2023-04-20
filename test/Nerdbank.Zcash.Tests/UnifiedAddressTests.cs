﻿// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public class UnifiedAddressTests : TestBase
{
    [Fact]
    public void Receivers_UnifiedMultiple()
    {
        UnifiedAddress addr = Assert.IsType<UnifiedAddress>(ZcashAddress.Parse(ValidUnifiedAddressOrchardSapling));
        Assert.Equal(
            new[]
            {
                ZcashAddress.Parse("u1v0j6szgvcquae449dltsrhdhlle4ac8cxd3z8k4j2wtxgfxg6xnq25a900d3yq65mz0l6heqhcj468f7q3l2wnxdsxjrcw90svum7q67"),
                ZcashAddress.Parse("zs1znewe2leucm8gsd2ue24kvp3jjjwgrhmytmv0scenaf460kdj70r299a88r8n0pyvwz7c9skfmy"),
            },
            addr.Receivers);

        addr = Assert.IsType<UnifiedAddress>(ZcashAddress.Parse(ValidUnifiedAddressOrchardSaplingTransparent));
        Assert.Equal(
            new[]
            {
                ZcashAddress.Parse("u1v0j6szgvcquae449dltsrhdhlle4ac8cxd3z8k4j2wtxgfxg6xnq25a900d3yq65mz0l6heqhcj468f7q3l2wnxdsxjrcw90svum7q67"),
                ZcashAddress.Parse("zs1znewe2leucm8gsd2ue24kvp3jjjwgrhmytmv0scenaf460kdj70r299a88r8n0pyvwz7c9skfmy"),
                ZcashAddress.Parse("t1a7w3qM23i4ajQcbX5wd6oH4zTY8Bry5vF"),
            },
            addr.Receivers);
    }
}
