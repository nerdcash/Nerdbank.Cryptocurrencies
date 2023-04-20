// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Nerdbank.Zcash;

/// <summary>
/// A transparent Zcash address.
/// </summary>
public class TransparentAddress : ZcashAddress
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TransparentAddress"/> class.
    /// </summary>
    /// <param name="address"><inheritdoc cref="ZcashAddress.ZcashAddress(ReadOnlySpan{char})" path="/param"/></param>
    internal TransparentAddress(ReadOnlySpan<char> address)
        : base(address)
    {
    }
}
