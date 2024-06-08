// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash;

/// <summary>
/// A transparent address with a "Pay to Public Key Hash" (P2PKH) receiver.
/// </summary>
[DebuggerDisplay($"{{{nameof(Address)},nq}}")]
public class TransparentP2PKHAddress : TransparentAddress
{
	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	private readonly TransparentP2PKHReceiver receiver;
	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	private readonly ZcashNetwork network;

	/// <inheritdoc cref="TransparentP2PKHAddress(string, in TransparentP2PKHReceiver, ZcashNetwork)"/>
	public TransparentP2PKHAddress(in TransparentP2PKHReceiver receiver, ZcashNetwork network = ZcashNetwork.MainNet)
		: base(CreateAddress(receiver, network))
	{
		this.receiver = receiver;
		this.network = network;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="TransparentP2PKHAddress"/> class.
	/// </summary>
	/// <param name="address"><inheritdoc cref="ZcashAddress(string)" path="/param"/></param>
	/// <param name="receiver">The encoded receiver.</param>
	/// <param name="network">The network to which this address belongs.</param>
	internal TransparentP2PKHAddress(string address, in TransparentP2PKHReceiver receiver, ZcashNetwork network)
		: base(address)
	{
		this.receiver = receiver;
		this.network = network;
	}

	/// <inheritdoc/>
	public override ZcashNetwork Network => this.network;

	/// <inheritdoc/>
	public override bool HasShieldedReceiver => false;

	/// <inheritdoc/>
	internal override byte UnifiedTypeCode => UnifiedTypeCodes.TransparentP2PKH;

	/// <inheritdoc/>
	internal override int ReceiverEncodingLength => TransparentP2PKHReceiver.Length;

	/// <inheritdoc/>
	public override TPoolReceiver? GetPoolReceiver<TPoolReceiver>() => AsReceiver<TransparentP2PKHReceiver, TPoolReceiver>(this.receiver);

	/// <inheritdoc/>
	internal override int GetReceiverEncoding(Span<byte> output) => this.receiver.Encode(output);

	private static string CreateAddress(in TransparentP2PKHReceiver receiver, ZcashNetwork network)
	{
		Span<byte> input = stackalloc byte[2 + TransparentP2PKHReceiver.Length];
		(input[0], input[1]) = network switch
		{
			ZcashNetwork.MainNet => ((byte)0x1c, (byte)0xb8),
			ZcashNetwork.TestNet => ((byte)0x1d, (byte)0x25),
			_ => throw new NotSupportedException(Strings.FormatUnrecognizedNetwork(network)),
		};
		receiver[..].CopyTo(input[2..]);
		Span<char> addressChars = stackalloc char[Base58Check.GetMaxEncodedLength(input.Length)];
		int charsLength = Base58Check.Encode(input, addressChars);
		return addressChars[..charsLength].ToString();
	}
}
