﻿// Copyright (c) Andrew Arnott. All rights reserved.
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

    /// <inheritdoc/>
    public override ZcashNetwork Network => throw new NotImplementedException();

    /// <summary>
    /// Gets the receivers for this address, in order of preference.
    /// </summary>
    /// <remarks>
    /// <para>Every address has at least one receiver, if it is valid. Non-unified addresses will simply enumerate themselves.</para>
    /// </remarks>
    public IEnumerable<ZcashAddress> Receivers => throw new NotImplementedException();

    /// <summary>
    /// Creates a unified address from a list of receiver addresses.
    /// </summary>
    /// <param name="receivers">
    /// The receivers to build into the unified address. This list should be in preferred order.
    /// At least one of these must be a shielded address.
    /// At most one of these can be a transparent address.
    /// Sprout addresses are not supported.
    /// </param>
    /// <returns>A unified address that contains all the receivers.</returns>
    public static UnifiedAddress Create(IReadOnlyList<ZcashAddress> receivers)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public override bool SupportsPool(Pool pool)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    protected override int DecodeAddress(Span<byte> rawEncoding) => throw new NotImplementedException();

    /// <inheritdoc/>
    protected override bool CheckValidity(bool throwIfInvalid = false)
    {
        // TODO: implement this.
        return true;
    }
}