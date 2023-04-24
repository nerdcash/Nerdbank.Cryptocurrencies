// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash;

/// <summary>
/// A shielded Zcash address belonging to the <see cref="Pool.Sprout"/> pool.
/// </summary>
public class SproutAddress : ZcashAddress
{
    private readonly SproutReceiver receiver;

    public SproutAddress(SproutReceiver receiver, ZcashNetwork network = ZcashNetwork.MainNet)
        : base(CreateAddress(receiver, network))
    {
        this.receiver = receiver;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SproutAddress"/> class.
    /// </summary>
    /// <param name="address"><inheritdoc cref="ZcashAddress.ZcashAddress(ReadOnlySpan{char})" path="/param"/></param>
    internal SproutAddress(ReadOnlySpan<char> address)
        : base(address)
    {
        this.receiver = CreateReceiver(address);
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
    private protected override int ReceiverEncodingLength => this.receiver.WholeThing.Length;

    /// <inheritdoc/>
    private protected override int GetReceiverEncoding(Span<byte> output)
    {
        this.receiver.WholeThing.CopyTo(output);
        return this.receiver.WholeThing.Length;
    }

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
    public override TPoolReceiver? GetPoolReceiver<TPoolReceiver>() => AsReceiver<SproutReceiver, TPoolReceiver>(this.receiver);

    /// <inheritdoc/>
    protected override bool CheckValidity(bool throwIfInvalid = false)
    {
        Span<byte> data = stackalloc byte[Base58Check.GetMaximumDecodedLength(this.Address.Length)];
        return Base58Check.TryDecode(this.Address, data, out _, out _, out _);
    }

    private static string CreateAddress(SproutReceiver receiver, ZcashNetwork network)
    {
        Span<byte> input = stackalloc byte[2 + receiver.WholeThing.Length];
        (input[0], input[1]) = network switch
        {
            ZcashNetwork.MainNet => ((byte)0x16, (byte)0x9a),
            ZcashNetwork.TestNet => ((byte)0x16, (byte)0xb6),
            _ => throw new NotSupportedException("Unrecognized network."),
        };
        receiver.WholeThing.CopyTo(input.Slice(2));
        Span<char> addressChars = stackalloc char[Base58Check.GetMaximumEncodedLength(input.Length)];
        int charsLength = Base58Check.Encode(input, addressChars);
        return addressChars.Slice(0, charsLength).ToString();
    }

    private static unsafe SproutReceiver CreateReceiver(ReadOnlySpan<char> address)
    {
        Span<byte> decoded = stackalloc byte[2 + sizeof(SproutReceiver)];
        Base58Check.Decode(address, decoded);
        return new SproutReceiver(decoded.Slice(2));
    }
}
