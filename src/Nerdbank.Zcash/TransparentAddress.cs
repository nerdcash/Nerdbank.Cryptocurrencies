// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash;

/// <summary>
/// A transparent Zcash address.
/// </summary>
public class TransparentAddress : ZcashAddress
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TransparentAddress"/> class.
    /// </summary>
    /// <param name="address"><inheritdoc cref="ZcashAddress(ReadOnlySpan{char})" path="/param"/></param>
    internal TransparentAddress(ReadOnlySpan<char> address)
        : base(address)
    {
    }

    /// <summary>
    /// Gets the type of this transparent address.
    /// </summary>
    public string Type => this.Address[1] switch
    {
        '1' => "P2PKH",
        '3' => "P2SH",
        _ => throw new InvalidOperationException("Invalid transparent address type"),
    };

    /// <inheritdoc/>
    public override ZcashNetwork Network
    {
        get
        {
            Span<byte> raw = stackalloc byte[this.Address.Length];
            int decodedLength = this.DecodeAddress(raw);
            return (raw[0], raw[1]) switch
            {
                (0x1c, 0xb8) or (0x1c, 0xbd) => ZcashNetwork.MainNet,
                (0x1d, 0x25) or (0x1c, 0xba) => ZcashNetwork.TestNet,
                _ => throw new InvalidOperationException("Invalid transparent address network"),
            };
        }
    }

    /// <inheritdoc/>
    public override bool SupportsPool(Pool pool) => pool == Pool.Transparent;

    /// <inheritdoc/>
    protected override int DecodeAddress(Span<byte> rawEncoding) => Base58Check.Decode(this.Address, rawEncoding);

    /// <inheritdoc/>
    protected override bool CheckValidity(bool throwIfInvalid = false)
    {
        Span<byte> raw = stackalloc byte[this.Address.Length];
        if (!Base58Check.TryDecode(this.Address, raw, out Base58Check.DecodeError? error, out string? errorMessage, out int decodedLength))
        {
            if (throwIfInvalid)
            {
                throw new FormatException(errorMessage);
            }

            return false;
        }

        if ((raw[0], raw[1]) is not ((0x1c, 0xb8) or (0x1c, 0xbd) or (0x1d, 0x25) or (0x1c, 0xba)))
        {
            if (throwIfInvalid)
            {
                throw new FormatException("Unrecognized header.");
            }

            return false;
        }

        return true;
    }
}
