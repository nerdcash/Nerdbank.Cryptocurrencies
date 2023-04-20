// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash;

/// <summary>
/// A Zcash address.
/// </summary>
public record struct ZcashAddress(string Address)
{
    /// <summary>
    /// Gets a value indicating whether this address is valid.
    /// </summary>
    public bool IsValid => throw new NotImplementedException();

    /// <summary>
    /// Gets the kind of address.
    /// </summary>
    public AddressKind Kind => throw new NotImplementedException();

    /// <summary>
    /// Gets the receivers for this address, in order of preference.
    /// </summary>
    /// <remarks>
    /// <para>Every address has at least one receiver, if it is valid. Non-unified addresses will simply enumerate themselves.</para>
    /// </remarks>
    public IEnumerable<ZcashAddress> Receivers => throw new NotImplementedException();

    /// <summary>
    /// Gets a value indicating whether this address is or contains an address for the specified pool.
    /// </summary>
    /// <param name="pool">The pool of interest.</param>
    /// <returns><see langword="true" /> if the address can receive funds for the <paramref name="pool"/>; otherwise <see langword="false"/>.</returns>
    public bool SupportsPool(Pool pool) => throw new NotImplementedException();
}
