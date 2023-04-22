// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash;

/// <summary>
/// A shielded Zcash address belonging to the <see cref="Pool.Sapling"/> pool.
/// </summary>
public class SaplingAddress : ZcashAddress
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SaplingAddress"/> class.
    /// </summary>
    /// <param name="address"><inheritdoc cref="ZcashAddress.ZcashAddress(ReadOnlySpan{char})" path="/param"/></param>
    internal SaplingAddress(ReadOnlySpan<char> address)
        : base(address)
    {
    }

    /// <inheritdoc/>
    public override ZcashNetwork Network =>
        this.Address.StartsWith("zs", StringComparison.Ordinal) ? ZcashNetwork.MainNet :
        this.Address.StartsWith("ztestsapling", StringComparison.Ordinal) ? ZcashNetwork.TestNet :
        throw new FormatException("Invalid address prefix");

    /// <summary>
    /// Gets the length of the buffers required to decode the address.
    /// </summary>
    /// <returns>The length of the human readable part and data buffers required.</returns>
    /// <exception cref="InvalidAddressException">Thrown if the address is invalid.</exception>
    internal (int HumanReadablePart, int Data) DecodedLength => Bech32.GetDecodedLength(this.Address) ?? throw new InvalidAddressException();

    /// <inheritdoc/>
    internal override byte UnifiedAddressTypeCode => 0x02;

    /// <inheritdoc/>
    private protected override int ReceiverEncodingLength => (Bech32.GetDecodedLength(this.Address) ?? throw new InvalidAddressException()).Data;

    /// <inheritdoc/>
    public override bool SupportsPool(Pool pool) => pool == Pool.Sapling;

    /// <summary>
    /// Decodes the address to its raw encoding.
    /// </summary>
    /// <param name="humanReadablePart">Receives the human-readable part of the address (e.g. "zs" or "ztestsapling").</param>
    /// <param name="data">Receives the raw encoding of the data within the address.</param>
    /// <returns>The actual length of the decoded bytes written to <paramref name="humanReadablePart"/> and <paramref name="data"/>.</returns>
    /// <exception cref="FormatException">Thrown if the address is invalid.</exception>
    internal (int HumanReadablePartLength, int DataLength) Decode(Span<char> humanReadablePart, Span<byte> data) => Bech32.Original.Decode(this.Address, humanReadablePart, data);

    /// <inheritdoc/>
    internal override int GetReceiverEncoding(Span<byte> destination)
    {
        (int Tag, int Data)? lengthPredicted = Bech32.GetDecodedLength(this.Address);
        if (lengthPredicted is null)
        {
            throw new InvalidAddressException();
        }

        Span<char> tag = stackalloc char[lengthPredicted.Value.Tag];
        Span<byte> data = stackalloc byte[lengthPredicted.Value.Data];
        (int TagLength, int DataLength) lengthWritten = Bech32.Original.Decode(this.Address, tag, data);
        data.Slice(0, lengthWritten.DataLength).CopyTo(destination);
        return lengthWritten.DataLength;
    }

    /// <inheritdoc/>
    protected override bool CheckValidity(bool throwIfInvalid = false)
    {
        (int Tag, int Data)? length = Bech32.GetDecodedLength(this.Address);
        if (length is null)
        {
            return false;
        }

        Span<char> tag = stackalloc char[length.Value.Tag];
        Span<byte> data = stackalloc byte[length.Value.Data];
        return Bech32.Original.TryDecode(this.Address, tag, data, out _, out _, out _);
    }
}
