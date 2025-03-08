// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash;

/// <summary>
/// A transparent Zcash address.
/// </summary>
public abstract class TransparentAddress : ZcashAddress
{
	/// <summary>
	/// Initializes a new instance of the <see cref="TransparentAddress"/> class.
	/// </summary>
	/// <param name="address"><inheritdoc cref="ZcashAddress(string)" path="/param"/></param>
	protected TransparentAddress(string address)
		: base(address)
	{
	}

	/// <summary>
	/// Gets the length of the buffer required to decode the address.
	/// </summary>
	internal static int DecodedLength => 22;

	/// <inheritdoc cref="ZcashAddress.TryDecode(string, out DecodeError?, out string?, out ZcashAddress?)" />
	internal static bool TryParse(string address, [NotNullWhen(false)] out DecodeError? errorCode, [NotNullWhen(false)] out string? errorMessage, [NotNullWhen(true)] out TransparentAddress? result)
	{
		if (TexAddress.LooksLikeTexAddress(address))
		{
			bool success = TexAddress.TryParse(address, out errorCode, out errorMessage, out TexAddress? texAddress);
			result = texAddress;
			return success;
		}
		else if (address.StartsWith("t", StringComparison.OrdinalIgnoreCase) && address.Length > 2)
		{
			Span<byte> decoded = stackalloc byte[DecodedLength];
			if (!Base58Check.TryDecode(address, decoded, out errorCode, out errorMessage, out _))
			{
				result = null;
				return false;
			}

#pragma warning disable SA1010 // Opening square brackets should be spaced correctly (https://github.com/DotNetAnalyzers/StyleCopAnalyzers/issues/3503)
			ZcashNetwork? network = decoded[..2] switch
			{
			[0x1c, 0xbd] or [0x1c, 0xb8] => ZcashNetwork.MainNet,
			[0x1c, 0xba] or [0x1d, 0x25] => ZcashNetwork.TestNet,
				_ => null,
			};

			if (network is null)
			{
				errorCode = DecodeError.UnrecognizedAddressType;
				errorMessage = Strings.InvalidNetworkHeader;
				result = null;
				return false;
			}

			result = decoded[..2] switch
			{
			[0x1c, 0xb8] or [0x1d, 0x25] => new TransparentP2PKHAddress(address, new TransparentP2PKHReceiver(decoded[2..]), network.Value),
			[0x1c, 0xbd] or [0x1c, 0xba] => new TransparentP2SHAddress(address, new TransparentP2SHReceiver(decoded[2..]), network.Value),
				_ => null,
			};
#pragma warning restore SA1010 // Opening square brackets should be spaced correctly

			if (result is not null)
			{
				errorMessage = null;
				errorCode = null;
				return true;
			}
		}

		result = null;
		errorCode = DecodeError.UnrecognizedAddressType;
		errorMessage = Strings.UnrecognizedAddress;
		return false;
	}
}
