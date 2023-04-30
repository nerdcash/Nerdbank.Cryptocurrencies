// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Cryptocurrencies;

/// <summary>
/// Static operations that can be performed bitwise on integers.
/// </summary>
internal static class BitUtilities
{
	/// <summary>
	/// Returns a bitmask that exposes the specified number of bits in a byte, starting with the most significant bit.
	/// </summary>
	/// <param name="msbBits">The number of bits to expose.</param>
	/// <returns>The bitmask.</returns>
	internal static byte MaskMSB(int msbBits) => (byte)~MaskLSB(8 - msbBits);

	/// <summary>
	/// Returns a bitmask that exposes the specified number of bits in a byte, starting with the least significant bit.
	/// </summary>
	/// <param name="lsbBits">The number of bits to expose.</param>
	/// <returns>The bitmask.</returns>
	internal static byte MaskLSB(int lsbBits)
	{
		Requires.Range(lsbBits >= 0 && lsbBits <= 8, nameof(lsbBits));
		return (byte)((1 << lsbBits) - 1);
	}
}
