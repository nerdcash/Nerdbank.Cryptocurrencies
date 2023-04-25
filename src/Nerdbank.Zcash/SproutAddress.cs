// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace Nerdbank.Zcash;

/// <summary>
/// A shielded Zcash address belonging to the <see cref="Pool.Sprout"/> pool.
/// </summary>
public class SproutAddress : ZcashAddress
{
	private readonly SproutReceiver receiver;
	private readonly ZcashNetwork network;

	/// <inheritdoc cref="SproutAddress(string, in SproutReceiver, ZcashNetwork)"/>
	public SproutAddress(in SproutReceiver receiver, ZcashNetwork network = ZcashNetwork.MainNet)
		: base(CreateAddress(receiver, network))
	{
		this.receiver = receiver;
		this.network = network;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SproutAddress"/> class.
	/// </summary>
	/// <param name="address"><inheritdoc cref="ZcashAddress(string)" path="/param"/></param>
	/// <param name="receiver">The encoded receiver.</param>
	/// <param name="network">The network to which this address belongs.</param>
	private SproutAddress(string address, in SproutReceiver receiver, ZcashNetwork network = ZcashNetwork.MainNet)
		: base(address)
	{
		this.receiver = receiver;
		this.network = network;
	}

	/// <inheritdoc/>
	public override ZcashNetwork Network => this.network;

	/// <summary>
	/// Gets the length of the buffer required to decode the address.
	/// </summary>
	internal int DecodedLength => Base58Check.GetMaximumDecodedLength(this.Address.Length);

	/// <inheritdoc/>
	internal override byte UnifiedAddressTypeCode => throw new NotSupportedException();

	/// <inheritdoc/>
	internal override int ReceiverEncodingLength => this.receiver.Span.Length;

	/// <inheritdoc/>
	public override TPoolReceiver? GetPoolReceiver<TPoolReceiver>() => AsReceiver<SproutReceiver, TPoolReceiver>(this.receiver);

	/// <inheritdoc cref="ZcashAddress.TryParse(string, out ZcashAddress?, out ParseError?, out string?)" />
	internal static bool TryParse(string address, [NotNullWhen(true)] out SproutAddress? result, [NotNullWhen(false)] out ParseError? errorCode, [NotNullWhen(false)] out string? errorMessage)
	{
		ZcashNetwork? network =
			address.StartsWith("zc", StringComparison.Ordinal) ? ZcashNetwork.MainNet :
			address.StartsWith("zt", StringComparison.Ordinal) ? ZcashNetwork.TestNet :
			null;
		if (network is null)
		{
			result = null;
			errorCode = ParseError.UnrecognizedAddressType;
			errorMessage = "A sprout address must start with 'zc' or 'zt'.";
			return false;
		}

		if (!TryCreateReceiver(address, out SproutReceiver? receiver, out DecodeError? decodeError, out errorMessage))
		{
			result = null;
			errorCode = DecodeToParseError(decodeError);
			return false;
		}

		result = new(address, receiver.Value, network.Value);
		errorCode = null;
		errorMessage = null;
		return true;
	}

	/// <inheritdoc/>
	internal override int GetReceiverEncoding(Span<byte> output)
	{
		ReadOnlySpan<byte> receiverSpan = this.receiver.Span;
		receiverSpan.CopyTo(output);
		return receiverSpan.Length;
	}

	/// <summary>
	/// Decodes the address to its raw encoding.
	/// </summary>
	/// <param name="rawEncoding">Receives the raw encoding of the data within the address. This should be at least <see cref="DecodedLength"/> in size.</param>
	/// <returns>The actual length of the decoded bytes written to <paramref name="rawEncoding"/>.</returns>
	/// <exception cref="FormatException">Thrown if the address is invalid.</exception>
	internal int Decode(Span<byte> rawEncoding) => Base58Check.Decode(this.Address, rawEncoding);

	private static string CreateAddress(in SproutReceiver receiver, ZcashNetwork network)
	{
		ReadOnlySpan<byte> receiverSpan = receiver.Span;
		Span<byte> input = stackalloc byte[2 + receiverSpan.Length];
		(input[0], input[1]) = network switch
		{
			ZcashNetwork.MainNet => ((byte)0x16, (byte)0x9a),
			ZcashNetwork.TestNet => ((byte)0x16, (byte)0xb6),
			_ => throw new NotSupportedException("Unrecognized network."),
		};
		receiverSpan.CopyTo(input.Slice(2));
		Span<char> addressChars = stackalloc char[Base58Check.GetMaximumEncodedLength(input.Length)];
		int charsLength = Base58Check.Encode(input, addressChars);
		return addressChars.Slice(0, charsLength).ToString();
	}

	private static unsafe bool TryCreateReceiver(ReadOnlySpan<char> address, [NotNullWhen(true)] out SproutReceiver? receiver, [NotNullWhen(false)] out DecodeError? errorCode, [NotNullWhen(false)] out string? errorMessage)
	{
		Span<byte> decoded = stackalloc byte[2 + sizeof(SproutReceiver)];
		if (!Base58Check.TryDecode(address, decoded, out errorCode, out errorMessage, out _))
		{
			receiver = null;
			return false;
		}

		receiver = new SproutReceiver(decoded[2..]);
		return true;
	}
}
