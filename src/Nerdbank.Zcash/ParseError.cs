// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash.Utilities;

/// <summary>
/// Enumerates the kinds of errors that can occur when parsing an address.
/// </summary>
public enum ParseError
{
    /// <summary>
    /// The address did not conform to a recognized type.
    /// </summary>
    UnrecognizedAddressType,

    /// <summary>
    /// The address violated an internal consistency check.
    /// </summary>
    InvalidAddress,
}
