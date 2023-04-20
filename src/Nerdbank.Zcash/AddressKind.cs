// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash;

/// <summary>
/// The kinds of Zcash addresses.
/// </summary>
public enum AddressKind
{
    /// <summary>
    /// The address is invalid and cannot be classified as any valid address kind.
    /// </summary>
    Invalid,

    /// <summary>
    /// An address that sends funds to the <see cref="Pool.Transparent"/> pool.
    /// </summary>
    Transparent,

    /// <summary>
    /// An address that sends funds to the <see cref="Pool.Sprout"/> pool.
    /// </summary>
    Sprout,

    /// <summary>
    /// An address that sends funds to the <see cref="Pool.Sapling"/> pool.
    /// </summary>
    Sapling,

    /// <summary>
    /// An address that sends funds to one or more pools based on the receivers that are embedded in the address.
    /// </summary>
    Unified,
}
