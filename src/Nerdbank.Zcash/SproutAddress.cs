// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash;

/// <summary>
/// A shielded Zcash address belonging to the <see cref="Pool.Sprout"/> pool.
/// </summary>
public class SproutAddress : ZcashAddress
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SproutAddress"/> class.
    /// </summary>
    /// <param name="address"><inheritdoc cref="ZcashAddress.ZcashAddress(ReadOnlySpan{char})" path="/param"/></param>
    internal SproutAddress(ReadOnlySpan<char> address)
        : base(address)
    {
    }

    /// <inheritdoc/>
    public override ZcashNetwork Network => throw new NotImplementedException();

    /// <summary>
    /// Gets the length of the buffer required to decode the address.
    /// </summary>
    internal int DecodedLength => Base58Check.GetMaximumDecodedLength(this.Address.Length);

    /// <inheritdoc/>
    public override bool SupportsPool(Pool pool) => pool == Pool.Sprout;

    /// <summary>
    /// Decodes the address to its raw encoding.
    /// </summary>
    /// <param name="rawEncoding">Receives the raw encoding of the data within the address. This should be at least <see cref="DecodedLength"/> in size.</param>
    /// <returns>The actual length of the decoded bytes written to <paramref name="rawEncoding"/>.</returns>
    /// <exception cref="FormatException">Thrown if the address is invalid.</exception>
    internal int Decode(Span<byte> rawEncoding) => Base58Check.Decode(this.Address, rawEncoding);

    /// <inheritdoc/>
    protected override bool CheckValidity(bool throwIfInvalid = false)
    {
        // TODO: implement this.
        return true;
    }
}
