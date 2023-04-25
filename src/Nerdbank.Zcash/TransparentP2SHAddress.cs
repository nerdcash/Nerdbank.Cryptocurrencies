// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash;

/// <summary>
/// A transparent address with a "Pay to Script Hash" (P2SH) receiver.
/// </summary>
public class TransparentP2SHAddress : TransparentAddress
{
	private readonly TransparentP2SHReceiver receiver;
	private readonly ZcashNetwork network;

	/// <inheritdoc cref="TransparentP2SHAddress(string, in TransparentP2SHReceiver, ZcashNetwork)"/>
	public TransparentP2SHAddress(in TransparentP2SHReceiver receiver, ZcashNetwork network = ZcashNetwork.MainNet)
		: base(CreateAddress(receiver, network))
	{
		this.receiver = receiver;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="TransparentP2SHAddress"/> class.
	/// </summary>
	/// <param name="address"><inheritdoc cref="ZcashAddress(string)" path="/param"/></param>
	/// <param name="receiver">The encoded receiver.</param>
	/// <param name="network">The network to which this address belongs.</param>
	internal TransparentP2SHAddress(string address, in TransparentP2SHReceiver receiver, ZcashNetwork network)
		: base(address)
	{
		this.receiver = receiver;
		this.network = network;
	}

	/// <inheritdoc/>
	public override ZcashNetwork Network => this.network;

	/// <inheritdoc/>
	internal override byte UnifiedAddressTypeCode => 0x01;

	/// <inheritdoc/>
	internal override int ReceiverEncodingLength => this.receiver.Span.Length;

	/// <inheritdoc/>
	public override TPoolReceiver? GetPoolReceiver<TPoolReceiver>() => AsReceiver<TransparentP2SHReceiver, TPoolReceiver>(this.receiver);

	/// <inheritdoc/>
	internal override int GetReceiverEncoding(Span<byte> output)
	{
		ReadOnlySpan<byte> receiverSpan = this.receiver.Span;
		receiverSpan.CopyTo(output);
		return receiverSpan.Length;
	}

	private static string CreateAddress(in TransparentP2SHReceiver receiver, ZcashNetwork network)
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
