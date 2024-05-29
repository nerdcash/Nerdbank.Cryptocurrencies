// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash;

/// <summary>
/// A transparent P2PKH address that has been encoded such that wallets should only send funds
/// to this address in a transaction funded <em>only</em> by transparent funds.
/// This means shielded funds must first be deshielded in a prior transaction before being sent to this address.
/// </summary>
/// <remarks>
/// <para>TEX addresses are designed for use by exchanges that require transparently funded accounts for compliance purposes
/// and so that they have an address to return rejected funds.</para>
/// <para>The matching receiver type for this address is <see cref="TransparentP2PKHReceiver"/>.</para>
/// <para>This implements <see href="https://zips.z.cash/zip-0320">ZIP-320</see>.</para>
/// </remarks>
public class TexAddress : TransparentAddress
{
	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	private readonly TransparentP2PKHReceiver receiver;
	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	private readonly ZcashNetwork network;

	/// <inheritdoc cref="TexAddress(string, in TransparentP2PKHReceiver, ZcashNetwork)"/>
	public TexAddress(in TransparentP2PKHReceiver receiver, ZcashNetwork network = ZcashNetwork.MainNet)
		: base(CreateAddress(receiver, network))
	{
		this.receiver = receiver;
		this.network = network;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="TexAddress"/> class
	/// based on a <see cref="TransparentP2PKHAddress"/>.
	/// </summary>
	/// <param name="transparentAddress">The transparent address that should only receive funds from transactions funded by transparent sources.</param>
	public TexAddress(TransparentP2PKHAddress transparentAddress)
		: this(Requires.NotNull(transparentAddress).GetPoolReceiver<TransparentP2PKHReceiver>()!.Value, transparentAddress.Network)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="TexAddress"/> class.
	/// </summary>
	/// <param name="address"><inheritdoc cref="ZcashAddress(string)" path="/param"/></param>
	/// <param name="receiver">The encoded receiver.</param>
	/// <param name="network">The network to which this address belongs.</param>
	internal TexAddress(string address, in TransparentP2PKHReceiver receiver, ZcashNetwork network)
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
	internal override byte UnifiedTypeCode => throw new NotSupportedException(Strings.AddressDoesNotSupportUnifiedEncoding);

	/// <inheritdoc/>
	internal override int ReceiverEncodingLength => this.receiver.EncodingLength;

	/// <inheritdoc/>
	public override TPoolReceiver? GetPoolReceiver<TPoolReceiver>() => AsReceiver<TransparentP2PKHReceiver, TPoolReceiver>(this.receiver);

	/// <summary>
	/// Checks whether a given string looks like a Zcash TEX address.
	/// </summary>
	/// <param name="address">The address to test.</param>
	/// <returns><see langword="true" /> if the address is likely a TEX address; otherwise <see langword="false" />.</returns>
	internal static bool LooksLikeTexAddress(ReadOnlySpan<char> address)
	{
		return
			(address.StartsWith(HumanReadablePart.MainNet, StringComparison.OrdinalIgnoreCase) && address[HumanReadablePart.MainNet.Length] == '1') ||
			(address.StartsWith(HumanReadablePart.TestNet, StringComparison.OrdinalIgnoreCase) && address[HumanReadablePart.TestNet.Length] == '1');
	}

	/// <inheritdoc cref="ZcashAddress.TryDecode(string, out DecodeError?, out string?, out ZcashAddress?)" />
	internal static bool TryParse(string address, [NotNullWhen(false)] out DecodeError? errorCode, [NotNullWhen(false)] out string? errorMessage, [NotNullWhen(true)] out TexAddress? result)
	{
		if (Bech32.GetDecodedLength(address) is (int tagLength, int dataLength))
		{
			Span<char> tag = stackalloc char[tagLength];
			Span<byte> data = stackalloc byte[dataLength];
			if (!Bech32.Bech32m.TryDecode(address, tag, data, out errorCode, out errorMessage, out _))
			{
				result = null;
				return false;
			}

			ZcashNetwork? network = tag switch
			{
				HumanReadablePart.MainNet => ZcashNetwork.MainNet,
				HumanReadablePart.TestNet => ZcashNetwork.TestNet,
				_ => null,
			};

			if (network is null)
			{
				errorCode = DecodeError.UnrecognizedHRP;
				errorMessage = $"Unexpected bech32 tag: {tag}";
				result = null;
				return false;
			}

			result = new TexAddress(address, new TransparentP2PKHReceiver(data), network.Value);
			return true;
		}

		errorCode = DecodeError.UnrecognizedAddressType;
		errorMessage = Strings.FormatInvalidXAddress("TEX");
		result = null;
		return false;
	}

	/// <inheritdoc/>
	internal override int GetReceiverEncoding(Span<byte> output) => this.receiver.Encode(output);

	private static string CreateAddress(in TransparentP2PKHReceiver receiver, ZcashNetwork network)
	{
		string hrp = network switch
		{
			ZcashNetwork.MainNet => HumanReadablePart.MainNet,
			ZcashNetwork.TestNet => HumanReadablePart.TestNet,
			_ => throw new NotSupportedException(Strings.FormatUnrecognizedNetwork(network)),
		};
		Span<char> addressChars = stackalloc char[Bech32.GetEncodedLength(hrp.Length, receiver.ValidatingKeyHash.Length)];
		int charsLength = Bech32.Bech32m.Encode(hrp, receiver.ValidatingKeyHash, addressChars);
		return addressChars[..charsLength].ToString();
	}

	private class HumanReadablePart
	{
		internal const string MainNet = "tex";
		internal const string TestNet = "textest";
	}
}
