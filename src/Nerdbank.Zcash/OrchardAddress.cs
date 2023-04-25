// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash;

/// <summary>
/// A Unified Address that carries a single Orchard receiver.
/// </summary>
public class OrchardAddress : UnifiedAddress
{
	private readonly OrchardReceiver receiver;
	private readonly ZcashNetwork network;

	/// <inheritdoc cref="OrchardAddress(string, in OrchardReceiver, ZcashNetwork)"/>
	public OrchardAddress(in OrchardReceiver receiver, ZcashNetwork network = ZcashNetwork.MainNet)
		: base(CreateAddress(receiver, network))
	{
		this.receiver = receiver;
		this.network = network;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="OrchardAddress"/> class.
	/// </summary>
	/// <param name="address"><inheritdoc cref="ZcashAddress(string)" path="/param"/></param>
	/// <param name="receiver">The encoded receiver.</param>
	/// <param name="network">The network to which this address belongs.</param>
	internal OrchardAddress(string address, in OrchardReceiver receiver, ZcashNetwork network = ZcashNetwork.MainNet)
		: base(address)
	{
		this.receiver = receiver;
		this.network = network;
	}

	/// <inheritdoc/>
	public override ZcashNetwork Network => this.network;

	/// <inheritdoc/>
	public override IReadOnlyList<ZcashAddress> Receivers => new[] { this };

	/// <inheritdoc/>
	internal override byte UnifiedAddressTypeCode => 0x03;

	/// <inheritdoc/>
	internal override int ReceiverEncodingLength => this.receiver.Span.Length;

	/// <inheritdoc/>
	public override TPoolReceiver? GetPoolReceiver<TPoolReceiver>() => AsReceiver<OrchardReceiver, TPoolReceiver>(this.receiver);

	/// <inheritdoc/>
	internal override int GetReceiverEncoding(Span<byte> output)
	{
		ReadOnlySpan<byte> receiverSpan = this.receiver.Span;
		receiverSpan.CopyTo(output);
		return receiverSpan.Length;
	}

	private static unsafe string CreateAddress(in OrchardReceiver receiver, ZcashNetwork network)
	{
		string humanReadablePart = network switch
		{
			ZcashNetwork.MainNet => HumanReadablePart,
			_ => throw new NotSupportedException("Unrecognized network."),
		};

		Span<byte> buffer = stackalloc byte[GetUAContributionLength<OrchardReceiver>() + Padding.Length];
		int written = 0;
		written += WriteUAContribution(receiver, buffer);
		Padding.CopyTo(buffer.Slice(written));
		written += Padding.Length;

		F4Jumble(buffer);

		Span<char> address = stackalloc char[Bech32.GetEncodedLength(humanReadablePart.Length, buffer.Length)];
		int finalLength = Bech32.Bech32m.Encode(humanReadablePart, buffer, address);
		Assumes.True(address.Length == finalLength);
		return new(address);
	}
}
