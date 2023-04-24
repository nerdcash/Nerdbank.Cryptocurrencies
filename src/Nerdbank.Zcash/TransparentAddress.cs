// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;

namespace Nerdbank.Zcash;

/// <summary>
/// A transparent Zcash address.
/// </summary>
public abstract class TransparentAddress : ZcashAddress
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
    /// Gets the length of the buffer required to decode the address.
    /// </summary>
    internal static int DecodedLength => 22;

    /// <inheritdoc/>
    public override bool SupportsPool(Pool pool) => pool == Pool.Transparent;

    internal static TransparentAddress? TryParse(ReadOnlySpan<char> address)
    {
        if (address.Length < 2)
        {
            return null;
        }

        Span<byte> decoded = stackalloc byte[DecodedLength];
        Base58Check.Decode(address, decoded);
        return decoded[..2] switch
        {
            [0x1c, 0xb8] or [0x1d, 0x25] => new TransparentP2PKHAddress(address),
            [0x1c, 0xbd] or [0x1c, 0xBA] => new TransparentP2SHAddress(address),
            _ => null,
        };
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

public class TransparentP2SHAddress : TransparentAddress
{
    private readonly TransparentP2SHReceiver receiver;

    public TransparentP2SHAddress(TransparentP2SHReceiver receiver, ZcashNetwork network = ZcashNetwork.MainNet)
        : base(CreateAddress(receiver, network))
    {
        this.receiver = receiver;
    }

    internal TransparentP2SHAddress(ReadOnlySpan<char> address)
        : base(address)
    {
        this.receiver = CreateReceiver(address);
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
                (0x1c, 0xbd) => ZcashNetwork.MainNet,
                (0x1c, 0xba) => ZcashNetwork.TestNet,
                _ => throw new InvalidAddressException(),
            };
        }
    }

    /// <inheritdoc/>
    internal override byte UnifiedAddressTypeCode => 0x01;

    /// <inheritdoc/>
    private protected override int ReceiverEncodingLength => this.receiver.WholeThing.Length;

    /// <inheritdoc/>
    private protected override int GetReceiverEncoding(Span<byte> output)
    {
        this.receiver.WholeThing.CopyTo(output);
        return this.receiver.WholeThing.Length;
    }

    public override TPoolReceiver? GetPoolReceiver<TPoolReceiver>() => AsReceiver<TransparentP2SHReceiver, TPoolReceiver>(this.receiver);

    private static string CreateAddress(TransparentP2SHReceiver receiver, ZcashNetwork network)
    {
        Span<byte> input = stackalloc byte[2 + receiver.ScriptHash.Length];
        (input[0], input[1]) = network switch
        {
            ZcashNetwork.MainNet => ((byte)0x1c, (byte)0xbd),
            ZcashNetwork.TestNet => ((byte)0x1c, (byte)0xba),
            _ => throw new NotSupportedException("Unrecognized network."),
        };
        receiver.ScriptHash.CopyTo(input.Slice(2));
        Span<char> addressChars = stackalloc char[Base58Check.GetMaximumEncodedLength(input.Length)];
        int charsLength = Base58Check.Encode(input, addressChars);
        return addressChars.Slice(0, charsLength).ToString();
    }

    private static TransparentP2SHReceiver CreateReceiver(ReadOnlySpan<char> address)
    {
        Span<byte> decoded = stackalloc byte[DecodedLength];
        Base58Check.Decode(address, decoded);
        return new TransparentP2SHReceiver(decoded.Slice(2));
    }
}

public class TransparentP2PKHAddress : TransparentAddress
{
    private readonly TransparentP2PKHReceiver receiver;

    public TransparentP2PKHAddress(TransparentP2PKHReceiver receiver, ZcashNetwork network = ZcashNetwork.MainNet)
        : base(CreateAddress(receiver, network))
    {
        this.receiver = receiver;
    }

    internal TransparentP2PKHAddress(ReadOnlySpan<char> address)
        : base(address)
    {
        this.receiver = CreateReceiver(address);
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
                (0x1c, 0xb8) => ZcashNetwork.MainNet,
                (0x1d, 0x25) => ZcashNetwork.TestNet,
                _ => throw new InvalidAddressException(),
            };
        }
    }

    /// <inheritdoc/>
    internal override byte UnifiedAddressTypeCode => 0x00;

    /// <inheritdoc/>
    private protected override int ReceiverEncodingLength => this.receiver.WholeThing.Length;

    /// <inheritdoc/>
    private protected override int GetReceiverEncoding(Span<byte> output)
    {
        this.receiver.WholeThing.CopyTo(output);
        return this.receiver.WholeThing.Length;
    }

    public override TPoolReceiver? GetPoolReceiver<TPoolReceiver>() => AsReceiver<TransparentP2PKHReceiver, TPoolReceiver>(this.receiver);

    private static string CreateAddress(TransparentP2PKHReceiver receiver, ZcashNetwork network)
    {
        Span<byte> input = stackalloc byte[2 + receiver.ValidatingKeyHash.Length];
        (input[0], input[1]) = network switch
        {
            ZcashNetwork.MainNet => ((byte)0x1c, (byte)0xb8),
            ZcashNetwork.TestNet => ((byte)0x1d, (byte)0x25),
            _ => throw new NotSupportedException("Unrecognized network."),
        };
        receiver.ValidatingKeyHash.CopyTo(input.Slice(2));
        Span<char> addressChars = stackalloc char[Base58Check.GetMaximumEncodedLength(input.Length)];
        int charsLength = Base58Check.Encode(input, addressChars);
        return addressChars.Slice(0, charsLength).ToString();
    }

    private static TransparentP2PKHReceiver CreateReceiver(ReadOnlySpan<char> address)
    {
        Span<byte> decoded = stackalloc byte[DecodedLength];
        Base58Check.Decode(address, decoded);
        return new TransparentP2PKHReceiver(decoded.Slice(2));
    }
}
