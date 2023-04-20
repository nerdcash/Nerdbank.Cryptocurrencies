// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash;

/// <summary>
/// A shielded Zcash address belonging to the <see cref="Pool.Sapling"/> pool.
/// </summary>
public class SaplingAddress : ZcashAddress
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SaplingAddress"/> class.
    /// </summary>
    /// <param name="address"><inheritdoc cref="ZcashAddress.ZcashAddress(ReadOnlySpan{char})" path="/param"/></param>
    internal SaplingAddress(ReadOnlySpan<char> address)
        : base(address)
    {
    }

    /// <inheritdoc/>
    public override bool SupportsPool(Pool pool) => pool == Pool.Sapling;
}
