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
    /// An enumeration of the types of transparent addresses.
    /// </summary>
    public enum TransparentType
    {
        /// <summary>
        /// A Pay to Script Hash (P2SH) address.
        /// </summary>
        P2SH,

        /// <summary>
        /// A Pay to Public Key Hash (P2PKH) address.
        /// </summary>
        P2PKH,
    }

    /// <summary>
    /// Gets the type of this transparent address.
    /// </summary>
    /// <exception cref="InvalidAddressException">Thrown if the address is invalid.</exception>
    public TransparentType Type
    {
        get
        {
            Span<byte> raw = stackalloc byte[Base58Check.GetMaximumDecodedLength(this.Address.Length)];
            this.Decode(raw);
            return (raw[0], raw[1]) switch
            {
                (0x1c, 0xba) or (0x1c, 0xbd) => TransparentType.P2SH,
                (0x1c, 0xb8) or (0x1d, 0x25) => TransparentType.P2PKH,
                _ => throw new InvalidAddressException(),
            };
        }
    }

    /// <inheritdoc/>
    public override ZcashNetwork Network
    {
        get
        {
            Span<byte> raw = stackalloc byte[22];
            this.Decode(raw);
            return (raw[0], raw[1]) switch
            {
                (0x1c, 0xb8) or (0x1c, 0xbd) => ZcashNetwork.MainNet,
                (0x1d, 0x25) or (0x1c, 0xba) => ZcashNetwork.TestNet,
                _ => throw new InvalidAddressException(),
            };
        }
    }

    /// <inheritdoc/>
    internal override byte UnifiedAddressTypeCode => this.Type switch
    {
        TransparentType.P2SH => 0x01,
        TransparentType.P2PKH => 0x00,
        _ => throw new NotImplementedException(),
    };

    /// <summary>
    /// Gets the length of the buffer required to decode the address.
    /// </summary>
    internal int DecodedLength => 22;

    /// <inheritdoc/>
    private protected override int ReceiverEncodingLength => 160 / 8;

    /// <inheritdoc/>
    public override bool SupportsPool(Pool pool) => pool == Pool.Transparent;

    /// <summary>
    /// Decodes the address to its raw encoding.
    /// </summary>
    /// <param name="rawEncoding">
    /// Receives the raw encoding of the data within the address. This should be at least <see cref="DecodedLength"/> in length.
    /// This will include the 2 byte header and the 20 byte hash of the script or validating key.
    /// </param>
    /// <returns>The actual length of the decoded bytes written to <paramref name="rawEncoding"/>.</returns>
    /// <exception cref="FormatException">Thrown if the address is invalid.</exception>
    internal int Decode(Span<byte> rawEncoding) => Base58Check.Decode(this.Address, rawEncoding);

    /// <inheritdoc/>
    internal override int GetReceiverEncoding(Span<byte> destination)
    {
        Span<byte> receiver = stackalloc byte[this.DecodedLength];
        this.Decode(receiver);
        receiver.Slice(2).CopyTo(destination);
        return receiver.Length - 2;
    }

    /// <inheritdoc/>
    protected override bool CheckValidity(bool throwIfInvalid = false)
    {
        Span<byte> raw = stackalloc byte[this.Address.Length];
        if (!Base58Check.TryDecode(this.Address, raw, out DecodeError? error, out string? errorMessage, out int decodedLength))
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
