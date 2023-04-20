// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash;

/// <summary>
/// A Zcash address.
/// </summary>
public record ZcashAddress(string Address)
{
    public AddressKind Kind { get; private init; }

    /// <summary>
    /// Gets a value indicating whether this address is or contains an address for the specified pool.
    /// </summary>
    /// <param name="pool">The pool of interest.</param>
    /// <returns><see langword="true" /> if the address can receive funds for the <paramref name="pool"/>; otherwise <see langword="false"/>.</returns>
    public bool SupportsPool(Pool pool) => throw new NotImplementedException();

    public static ZcashAddress Parse(ReadOnlySpan<char> address)
    {
        return new();
    }
}
