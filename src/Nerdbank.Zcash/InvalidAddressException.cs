// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash;

/// <summary>
/// An exception thrown when an address is invalid.
/// </summary>
public class InvalidAddressException : Exception
{
    /// <inheritdoc cref="InvalidAddressException(string?, Exception?)"/>
    public InvalidAddressException()
        : this("The address is invalid.")
    {
    }

    /// <inheritdoc cref="InvalidAddressException(string?, Exception?)"/>
    public InvalidAddressException(string? message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidAddressException"/> class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="inner">An inner exception.</param>
    public InvalidAddressException(string? message, Exception? inner)
        : base(message, inner)
    {
    }
}
