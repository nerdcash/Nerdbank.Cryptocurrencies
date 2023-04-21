// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft;

namespace Nerdbank.Zcash;

/// <summary>
/// A <see href="https://zips.z.cash/zip-0316">unified Zcash address</see>.
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
    /// Gets a value indicating whether this address is a raw Orchard address rather than an address with receivers.
    /// </summary>
    internal bool IsOrchardRaw => throw new NotImplementedException();

    /// <summary>
    /// Creates a unified address from a list of receiver addresses.
    /// </summary>
    /// <param name="receivers">
    /// The receivers to build into the unified address.
    /// These will be sorted by preferred order before being encoded into the address.
    /// No more than one of each type of address is allowed.
    /// Sprout addresses are not allowed.
    /// </param>
    /// <returns>A unified address that contains all the receivers.</returns>
    public static UnifiedAddress Create(IReadOnlyCollection<ZcashAddress> receivers)
    {
        ZcashAddress? orchard = null;
        ZcashAddress? sapling = null;
        ZcashAddress? transparent = null;

        void AssignOrThrow(ref ZcashAddress? location, ZcashAddress value)
        {
            if (location is null)
            {
                location = value;
            }
            else
            {
                throw new ArgumentException("Only one of each type of address is allowed.", nameof(receivers));
            }
        }

        bool hasShieldedAddress = false;
        foreach (ZcashAddress receiver in receivers)
        {
            // Pattern matching would be amazing, but https://github.com/dotnet/csharplang/discussions/7133
            if (receiver is UnifiedAddress { IsOrchardRaw: true })
            {
                AssignOrThrow(ref orchard, receiver);
                hasShieldedAddress = true;
            }
            else if (receiver is SaplingAddress)
            {
                AssignOrThrow(ref sapling, receiver);
                hasShieldedAddress = true;
            }
            else if (receiver is TransparentAddress)
            {
                AssignOrThrow(ref transparent, receiver);
            }
            else
            {
                throw new ArgumentException("Only Orchard, Sapling, and Transparent addresses are supported.", nameof(receivers));
            }
        }

        Requires.Argument(hasShieldedAddress, nameof(receivers), "At least one shielded address is required.");

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
