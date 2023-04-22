﻿// Copyright (c) Andrew Arnott. All rights reserved.
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
    public override ZcashNetwork Network =>
        this.Address.StartsWith("zc", StringComparison.Ordinal) ? ZcashNetwork.MainNet :
        this.Address.StartsWith("zt", StringComparison.Ordinal) ? ZcashNetwork.TestNet :
        throw new FormatException("Invalid address prefix");

    /// <summary>
    /// Gets the length of the buffer required to decode the address.
    /// </summary>
    internal int DecodedLength => Base58Check.GetMaximumDecodedLength(this.Address.Length);

    /// <inheritdoc/>
    internal override byte UnifiedAddressTypeCode => throw new NotSupportedException();

    /// <inheritdoc/>
    private protected override int ReceiverEncodingLength => throw new NotSupportedException();

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
    internal override int GetReceiverEncoding(Span<byte> destination) => throw new NotSupportedException();

    /// <inheritdoc/>
    protected override bool CheckValidity(bool throwIfInvalid = false)
    {
        Span<byte> data = stackalloc byte[Base58Check.GetMaximumDecodedLength(this.Address.Length)];
        return Base58Check.TryDecode(this.Address, data, out _, out _, out _);
    }
}
