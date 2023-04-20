// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Nerdbank.Zcash;

/// <summary>
/// A Zcash address.
/// </summary>
public abstract class ZcashAddress : IEquatable<ZcashAddress>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ZcashAddress"/> class.
    /// </summary>
    /// <param name="address">The address in string form.</param>
    protected ZcashAddress(ReadOnlySpan<char> address)
    {
        this.Address = address.ToString().ToLowerInvariant();
    }

    /// <summary>
    /// Gets the address as a string.
    /// </summary>
    protected string Address { get; }

    /// <summary>
    /// Parse a string of characters as an address.
    /// </summary>
    /// <param name="address">The address.</param>
    /// <returns>The parsed address.</returns>
    /// <exception type="ArgumentException">Thrown if the address is invalid.</exception>
    public static ZcashAddress Parse(ReadOnlySpan<char> address)
    {
        if (!TryParse(address, out ZcashAddress? result))
        {
            throw new ArgumentException();
        }

        return result;
    }

    /// <summary>
    /// Tries to parse a string of characters as an address.
    /// </summary>
    /// <param name="address">The address.</param>
    /// <param name="result">Receives the parsed address.</param>
    /// <returns>A value indicating whether the address parsed to a valid address.</returns>
    public static bool TryParse(ReadOnlySpan<char> address, [NotNullWhen(true)] out ZcashAddress? result)
    {
        if (address.Length < 2)
        {
            throw new ArgumentException();
        }

        result = char.ToLowerInvariant(address[0]) switch
        {
            't' => new TransparentAddress(address),
            'z' => char.ToLowerInvariant(address[1]) switch
            {
                'c' => new SproutAddress(address),
                's' => new SaplingAddress(address),
                _ => null,
            },
            'u' => new UnifiedAddress(address),
            _ => null,
        };

        return result is not null;
    }

    /// <summary>
    /// Gets a value indicating whether this address is or contains an address for the specified pool.
    /// </summary>
    /// <param name="pool">The pool of interest.</param>
    /// <returns><see langword="true" /> if the address can receive funds for the <paramref name="pool"/>; otherwise <see langword="false"/>.</returns>
    public bool SupportsPool(Pool pool) => throw new NotImplementedException();

    /// <summary>
    /// Returns the zcash address.
    /// </summary>
    /// <returns>The address.</returns>
    public override string ToString() => this.Address;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => this.Equals(obj as ZcashAddress);

    /// <inheritdoc/>
    public override int GetHashCode() => this.Address.GetHashCode();

    /// <inheritdoc/>
    public bool Equals(ZcashAddress? other) => this == other || this.Address == other?.Address;
}
