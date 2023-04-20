// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash;

/// <summary>
/// A unified Zcash address.
/// </summary>
public class UnifiedAddress : ZcashAddress
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UnifiedAddress"/> class.
    /// </summary>
    /// <param name="address"><inheritdoc cref="ZcashAddress.ZcashAddress(ReadOnlySpan{char})" path="/param"/></param>
    internal UnifiedAddress(ReadOnlySpan<char> address)
        : base(address)
    {
    }

    /// <summary>
    /// Gets the receivers for this address, in order of preference.
    /// </summary>
    /// <remarks>
    /// <para>Every address has at least one receiver, if it is valid. Non-unified addresses will simply enumerate themselves.</para>
    /// </remarks>
    public IEnumerable<ZcashAddress> Receivers => throw new NotImplementedException();

    /// <inheritdoc/>
    public override bool SupportsPool(Pool pool)
    {
        throw new NotImplementedException();
    }
}
