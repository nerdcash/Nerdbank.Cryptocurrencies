// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash;

/// <summary>
/// A shielded Zcash address belonging to the <see cref="Pool.Sprout"/> pool.
/// </summary>
[DebuggerDisplay($"{{{nameof(Address)},nq}}")]
public class SproutAddress : ZcashAddress
{
	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	private readonly SproutReceiver receiver;
	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
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

	/// <inheritdoc/>
	public override bool HasShieldedReceiver => true;

	/// <inheritdoc/>
	internal override byte UnifiedTypeCode => throw new NotSupportedException();

	/// <inheritdoc/>
	[ExcludeFromCodeCoverage]
	internal override int ReceiverEncodingLength
	{
		get
		{
			// This method is only called by UAs, which don't support sprout addresses.
			throw new NotImplementedException();
		}
	}

	/// <inheritdoc/>
	public override TPoolReceiver? GetPoolReceiver<TPoolReceiver>() => AsReceiver<SproutReceiver, TPoolReceiver>(this.receiver);

	/// <inheritdoc cref="ZcashAddress.TryParse(string, out ZcashAddress?, out ParseError?, out string?)" />
	internal static unsafe bool TryParse(string address, [NotNullWhen(true)] out SproutAddress? result, [NotNullWhen(false)] out ParseError? errorCode, [NotNullWhen(false)] out string? errorMessage)
	{
		ZcashNetwork? network =
			address.StartsWith("zc", StringComparison.Ordinal) ? ZcashNetwork.MainNet :
			address.StartsWith("zt", StringComparison.Ordinal) ? ZcashNetwork.TestNet :
			null;
		if (network is null)
		{
			result = null;
			errorCode = ParseError.UnrecognizedAddressType;
			errorMessage = Strings.InvalidSproutPreamble;
			return false;
		}

		Span<byte> decoded = stackalloc byte[2 + sizeof(SproutReceiver)];
		if (!Base58Check.TryDecode(address, decoded, out DecodeError? decodeError, out errorMessage, out _))
		{
			result = null;
			errorCode = DecodeToParseError(decodeError);
			return false;
		}

		SproutReceiver receiver = new(decoded[2..]);

		result = new(address, receiver, network.Value);
		errorCode = null;
		errorMessage = null;
		return true;
	}

	/// <inheritdoc/>
	[ExcludeFromCodeCoverage]
	internal override int GetReceiverEncoding(Span<byte> output)
	{
		// This method is only called by UAs, which don't support sprout addresses.
		throw new NotImplementedException();
	}

	private static string CreateAddress(in SproutReceiver receiver, ZcashNetwork network)
	{
		ReadOnlySpan<byte> receiverSpan = receiver.Span;
		Span<byte> input = stackalloc byte[2 + receiverSpan.Length];
		(input[0], input[1]) = network switch
		{
			ZcashNetwork.MainNet => ((byte)0x16, (byte)0x9a),
			ZcashNetwork.TestNet => ((byte)0x16, (byte)0xb6),
			_ => throw new NotSupportedException(Strings.UnrecognizedNetwork),
		};
		receiverSpan.CopyTo(input[2..]);
		Span<char> addressChars = stackalloc char[Base58Check.GetMaxEncodedLength(input.Length)];
		int charsLength = Base58Check.Encode(input, addressChars);
		return addressChars[..charsLength].ToString();
	}
}
