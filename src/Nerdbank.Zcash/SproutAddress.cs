﻿// Copyright (c) IronPigeon, LLC. All rights reserved.
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
	internal override byte UnifiedTypeCode => throw new NotSupportedException(Strings.AddressDoesNotSupportUnifiedEncoding);

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

	/// <inheritdoc cref="ZcashAddress.TryDecode(string, out DecodeError?, out string?, out ZcashAddress?)" />
	internal static unsafe bool TryParse(string address, [NotNullWhen(true)] out SproutAddress? result, [NotNullWhen(false)] out DecodeError? errorCode, [NotNullWhen(false)] out string? errorMessage)
	{
		ZcashNetwork? network =
			address.StartsWith("zc", StringComparison.Ordinal) ? ZcashNetwork.MainNet :
			address.StartsWith("zt", StringComparison.Ordinal) ? ZcashNetwork.TestNet :
			null;
		if (network is null)
		{
			result = null;
			errorCode = DecodeError.UnrecognizedAddressType;
			errorMessage = Strings.InvalidSproutPreamble;
			return false;
		}

		Span<byte> decoded = stackalloc byte[2 + sizeof(SproutReceiver)];
		if (!Base58Check.TryDecode(address, decoded, out errorCode, out errorMessage, out _))
		{
			result = null;
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
		Span<byte> input = stackalloc byte[2 + SproutReceiver.Length];
		(input[0], input[1]) = network switch
		{
			ZcashNetwork.MainNet => ((byte)0x16, (byte)0x9a),
			ZcashNetwork.TestNet => ((byte)0x16, (byte)0xb6),
			_ => throw new NotSupportedException(Strings.FormatUnrecognizedNetwork(network)),
		};
		receiver.Encode(input[2..]);
		Span<char> addressChars = stackalloc char[Base58Check.GetMaxEncodedLength(input.Length)];
		int charsLength = Base58Check.Encode(input, addressChars);
		return addressChars[..charsLength].ToString();
	}
}
