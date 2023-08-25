// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash;

/// <summary>
/// A shielded Zcash address belonging to the <see cref="Pool.Sapling"/> pool.
/// </summary>
[DebuggerDisplay($"{{{nameof(Address)},nq}}")]
public class SaplingAddress : ZcashAddress
{
	private const string MainNetHumanReadablePart = "zs";
	private const string TestNetHumanReadablePart = "ztestsapling";
	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	private readonly SaplingReceiver receiver;
	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	private readonly ZcashNetwork network;

	/// <inheritdoc cref="SaplingAddress(string, in SaplingReceiver, ZcashNetwork)"/>
	public SaplingAddress(in SaplingReceiver receiver, ZcashNetwork network = ZcashNetwork.MainNet)
		: base(CreateAddress(receiver, network))
	{
		this.network = network;
		this.receiver = receiver;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SaplingAddress"/> class.
	/// </summary>
	/// <param name="address"><inheritdoc cref="ZcashAddress(string)" path="/param"/></param>
	/// <param name="receiver">The encoded receiver.</param>
	/// <param name="network">The network to which this address belongs.</param>
	private SaplingAddress(string address, in SaplingReceiver receiver, ZcashNetwork network = ZcashNetwork.MainNet)
		: base(address)
	{
		this.network = network;
		this.receiver = receiver;
	}

	/// <inheritdoc/>
	public override ZcashNetwork Network => this.network;

	/// <inheritdoc/>
	public override bool HasShieldedReceiver => true;

	/// <inheritdoc/>
	internal override byte UnifiedTypeCode => UnifiedTypeCodes.Sapling;

	/// <inheritdoc/>
	internal override int ReceiverEncodingLength => this.receiver.Span.Length;

	/// <inheritdoc/>
	public override TPoolReceiver? GetPoolReceiver<TPoolReceiver>() => AsReceiver<SaplingReceiver, TPoolReceiver>(this.receiver);

	/// <inheritdoc cref="ZcashAddress.TryParse(string, out ZcashAddress?, out ParseError?, out string?)" />
	internal static bool TryParse(string address, [NotNullWhen(true)] out SaplingAddress? result, [NotNullWhen(false)] out ParseError? errorCode, [NotNullWhen(false)] out string? errorMessage)
	{
		ZcashNetwork? network =
			address.StartsWith(MainNetHumanReadablePart, StringComparison.Ordinal) ? ZcashNetwork.MainNet :
			address.StartsWith(TestNetHumanReadablePart, StringComparison.Ordinal) ? ZcashNetwork.TestNet :
			null;
		if (network is null)
		{
			result = null;
			errorCode = ParseError.UnrecognizedAddressType;
			errorMessage = Strings.InvalidSaplingPreamble;
			return false;
		}

		if (Bech32.GetDecodedLength(address) is (int tagLength, int dataLength))
		{
			Span<char> tag = stackalloc char[tagLength];
			Span<byte> data = stackalloc byte[dataLength];
			if (!Bech32.Original.TryDecode(address, tag, data, out DecodeError? decodeError, out errorMessage, out _))
			{
				result = null;
				errorCode = DecodeToParseError(decodeError);
				return false;
			}

			result = new SaplingAddress(address, new SaplingReceiver(data), network.Value);
			errorCode = null;
			errorMessage = null;
			return true;
		}

		result = null;
		errorCode = ParseError.UnrecognizedAddressType;
		errorMessage = Strings.FormatInvalidXAddress("sapling");
		return false;
	}

	/// <inheritdoc/>
	internal override int GetReceiverEncoding(Span<byte> output)
	{
		ReadOnlySpan<byte> receiverSpan = this.receiver.Span;
		receiverSpan.CopyTo(output);
		return receiverSpan.Length;
	}

	private static string CreateAddress(in SaplingReceiver receiver, ZcashNetwork network)
	{
		string humanReadablePart = network switch
		{
			ZcashNetwork.MainNet => MainNetHumanReadablePart,
			ZcashNetwork.TestNet => TestNetHumanReadablePart,
			_ => throw new NotSupportedException(Strings.UnrecognizedNetwork),
		};
		ReadOnlySpan<byte> receiverSpan = receiver.Span;
		Span<char> addressChars = stackalloc char[Bech32.GetEncodedLength(humanReadablePart.Length, receiverSpan.Length)];
		int charsLength = Bech32.Original.Encode(humanReadablePart, receiverSpan, addressChars);
		return addressChars[..charsLength].ToString();
	}
}
