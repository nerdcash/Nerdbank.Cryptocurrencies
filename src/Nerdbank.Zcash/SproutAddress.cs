// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash;

/// <summary>
/// A shielded Zcash address belonging to the <see cref="Pool.Sprout"/> pool.
/// </summary>
public class SproutAddress : ZcashAddress
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SproutAddress"/> class.
    /// </summary>
    /// <param name="address"><inheritdoc cref="ZcashAddress.ZcashAddress(ReadOnlySpan{char})" path="/param"/></param>
    internal SproutAddress(ReadOnlySpan<char> address)
        : base(address)
    {
    }

    /// <inheritdoc/>
    public override ZcashNetwork Network => throw new NotImplementedException();

    /// <inheritdoc/>
    public override bool SupportsPool(Pool pool) => pool == Pool.Sprout;

    /// <inheritdoc/>
    protected override int DecodeAddress(Span<byte> rawEncoding) => throw new NotImplementedException();

    /// <inheritdoc/>
    protected override bool CheckValidity(bool throwIfInvalid = false)
    {
        // TODO: implement this.
        return true;
    }
}
