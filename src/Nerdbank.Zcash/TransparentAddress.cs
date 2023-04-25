// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace Nerdbank.Zcash;

/// <summary>
/// A transparent Zcash address.
/// </summary>
public abstract class TransparentAddress : ZcashAddress
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TransparentAddress"/> class.
    /// </summary>
    /// <param name="address"><inheritdoc cref="ZcashAddress(string)" path="/param"/></param>
    internal TransparentAddress(string address)
        : base(address)
    {
    }

    /// <summary>
    /// Gets the length of the buffer required to decode the address.
    /// </summary>
    internal static int DecodedLength => 22;

    /// <inheritdoc cref="ZcashAddress.TryParse(string, out ZcashAddress?, out ParseError?, out string?)" />
    internal static bool TryParse(string address, [NotNullWhen(true)] out TransparentAddress? result, [NotNullWhen(false)] out ParseError? errorCode, [NotNullWhen(false)] out string? errorMessage)
    {
        if (address.StartsWith("t", StringComparison.OrdinalIgnoreCase) && address.Length > 2)
        {
            Span<byte> decoded = stackalloc byte[DecodedLength];
            if (!Base58Check.TryDecode(address, decoded, out DecodeError? decodeError, out errorMessage, out _))
            {
                result = null;
                errorCode = DecodeToParseError(decodeError);
                return false;
            }

#pragma warning disable SA1010 // Opening square brackets should be spaced correctly (https://github.com/DotNetAnalyzers/StyleCopAnalyzers/issues/3503)
            ZcashNetwork? network = decoded[..2] switch
            {
                [0x1c, 0xbd] or [0x1c, 0xb8] => ZcashNetwork.MainNet,
                [0x1c, 0xba] or [0x1d, 0x25] => ZcashNetwork.TestNet,
                _ => null,
            };

            if (network is null)
            {
                errorCode = ParseError.InvalidAddress;
                errorMessage = Strings.InvalidNetworkHeader;
                result = null;
                return false;
            }

            result = decoded[..2] switch
            {
                [0x1c, 0xb8] or [0x1d, 0x25] => new TransparentP2PKHAddress(address, new TransparentP2PKHReceiver(decoded[2..]), network.Value),
                [0x1c, 0xbd] or [0x1c, 0xba] => new TransparentP2SHAddress(address, new TransparentP2SHReceiver(decoded[2..]), network.Value),
                _ => null,
            };
#pragma warning restore SA1010 // Opening square brackets should be spaced correctly

            if (result is not null)
            {
                errorMessage = null;
                errorCode = null;
                return true;
            }
        }

        result = null;
        errorCode = ParseError.UnrecognizedAddressType;
        errorMessage = Strings.UnrecognizedAddress;
        return false;
    }

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
}
