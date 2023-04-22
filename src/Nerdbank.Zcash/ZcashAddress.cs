// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using Microsoft;

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
        this.Address = address.ToString();
    }

    /// <summary>
    /// Gets the network the address belongs to.
    /// </summary>
    /// <exception cref="InvalidAddressException">Thrown if the address is invalid.</exception>
    public abstract ZcashNetwork Network { get; }

    /// <summary>
    /// Gets a value indicating whether the address is valid.
    /// </summary>
    internal bool IsValid => this.CheckValidity();

    /// <summary>
    /// Gets the total length of this address's contribution to a unified address.
    /// </summary>
    internal int UAContributionLength => 1 + CompactSize.GetEncodedLength((ulong)this.ReceiverEncodingLength) + this.ReceiverEncodingLength;

    /// <summary>
    /// Gets the type code to use when embedded in a unified address.
    /// </summary>
    internal abstract byte UnifiedAddressTypeCode { get; }

    /// <summary>
    /// Gets the address as a string.
    /// </summary>
    protected string Address { get; }

    /// <summary>
    /// Gets the length of the receiver encoding in a unified address.
    /// </summary>
    private protected abstract int ReceiverEncodingLength { get; }

    /// <summary>
    /// Implicitly casts this address to a string.
    /// </summary>
    /// <param name="address">The address to convert.</param>
    public static implicit operator string(ZcashAddress address) => address.Address;

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
            // If we were able to at least recognize the address type, then allow it to throw an informative exception.
            result?.CheckValidity(throwIfInvalid: true);

            // It didn't throw, or it wasn't even a valid address type.
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
            result = null;
            return false;
        }

        result = char.ToLowerInvariant(address[0]) switch
        {
            't' => new TransparentAddress(address),
            'z' => char.ToLowerInvariant(address[1]) switch
            {
                'c' => new SproutAddress(address),
                's' => address.Length > 2 && address[2] == '1' ? new SaplingAddress(address) : null,
                't' => address.StartsWith("ztestsapling1", StringComparison.Ordinal) ? new SaplingAddress(address) : new SproutAddress(address),
                _ => null,
            },
            'u' => address[1] == '1' ? new UnifiedAddress(address) : null,
            _ => null,
        };

        return result?.IsValid is true;
    }

    /// <summary>
    /// Gets a value indicating whether this address is or contains an address for the specified pool.
    /// </summary>
    /// <param name="pool">The pool of interest.</param>
    /// <returns><see langword="true" /> if the address can receive funds for the <paramref name="pool"/>; otherwise <see langword="false"/>.</returns>
    public abstract bool SupportsPool(Pool pool);

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

    /// <summary>
    /// Gets the Receiver Encoding of this address for inclusion in a unified address.
    /// </summary>
    /// <param name="destination">The buffer to write the encoding to. Must be at least <see cref="ReceiverEncodingLength"/> in size.</param>
    /// <returns>The number of bytes written to the destination buffer.</returns>
    internal abstract int GetReceiverEncoding(Span<byte> destination);

    /// <summary>
    /// Writes this address's contribution to a unified address.
    /// </summary>
    /// <param name="destination">The buffer to receive the UA contribution.</param>
    /// <returns>The number of bytes actually written to the buffer.</returns>
    internal int WriteUAContribution(Span<byte> destination)
    {
        int bytesWritten = 0;
        destination[bytesWritten++] = this.UnifiedAddressTypeCode;
        int predictedEncodingLength = this.ReceiverEncodingLength;
        bytesWritten += CompactSize.Encode((ulong)predictedEncodingLength, destination);
        int actualEncodingLength = this.GetReceiverEncoding(destination.Slice(bytesWritten));
        Assumes.True(predictedEncodingLength == actualEncodingLength); // If this is wrong, we encoded the wrong length in the compact size.
        bytesWritten += actualEncodingLength;
        return bytesWritten;
    }

    /// <summary>
    /// Checks whether the address is valid.
    /// </summary>
    /// <param name="throwIfInvalid">A value indicating whether to throw an exception if the address is invalid.</param>
    /// <returns>A value indicating whether the address is invalid.</returns>
    /// <exception cref="FormatException">Thrown if <paramref name="throwIfInvalid"/> is <see langword="true" /> and the address is invalid.</exception>
    protected abstract bool CheckValidity(bool throwIfInvalid = false);
}
