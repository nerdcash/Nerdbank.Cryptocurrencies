// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash;

/// <summary>
/// A Unified Address that carries a single Orchard receiver.
/// </summary>
[DebuggerDisplay($"{{{nameof(Address)},nq}}")]
public class OrchardAddress : UnifiedAddress
{
	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	private readonly OrchardReceiver receiver;
	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	private readonly ZcashNetwork network;
	private ReadOnlyCollection<ZcashAddress>? receivers;

	/// <inheritdoc cref="OrchardAddress(string, in OrchardReceiver, ZcashNetwork)"/>
	public OrchardAddress(in OrchardReceiver receiver, ZcashNetwork network = ZcashNetwork.MainNet)
		: base(CreateAddress(receiver, network), revision: 0)
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
		: base(address, revision: 0)
	{
		this.receiver = receiver;
		this.network = network;
	}

	/// <inheritdoc/>
	public override ZcashNetwork Network => this.network;

	/// <inheritdoc/>
	public override bool HasShieldedReceiver => true;

	/// <inheritdoc/>
	public override IReadOnlyList<ZcashAddress> Receivers => this.receivers ??= new ReadOnlyCollection<ZcashAddress>(new[] { this });

	/// <inheritdoc/>
	internal override byte UnifiedTypeCode => UnifiedTypeCodes.Orchard;

	/// <inheritdoc/>
	internal override int ReceiverEncodingLength => OrchardReceiver.Length;

	/// <inheritdoc/>
	public override TPoolReceiver? GetPoolReceiver<TPoolReceiver>() => AsReceiver<OrchardReceiver, TPoolReceiver>(this.receiver);

	/// <inheritdoc/>
	internal override int GetReceiverEncoding(Span<byte> output) => this.receiver.Encode(output);

	private static unsafe string CreateAddress(in OrchardReceiver receiver, ZcashNetwork network)
	{
		string humanReadablePart = network switch
		{
			ZcashNetwork.MainNet => HumanReadablePart.R0.MainNet,
			ZcashNetwork.TestNet => HumanReadablePart.R0.TestNet,
			_ => throw new NotSupportedException(Strings.FormatUnrecognizedNetwork(network)),
		};

		Span<byte> padding = stackalloc byte[16];
		UnifiedEncoding.InitializePadding(humanReadablePart, padding);
		Span<byte> buffer = stackalloc byte[GetUAContributionLength<OrchardReceiver>() + padding.Length];
		int written = 0;
		written += WriteUAContribution(receiver, buffer);
		padding.CopyTo(buffer[written..]);
		written += padding.Length;

		UnifiedEncoding.F4Jumble(buffer);

		Span<char> address = stackalloc char[Bech32.GetEncodedLength(humanReadablePart.Length, buffer.Length)];
		int finalLength = Bech32.Bech32m.Encode(humanReadablePart, buffer, address);
		Assumes.True(address.Length == finalLength);
		return new(address);
	}
}
