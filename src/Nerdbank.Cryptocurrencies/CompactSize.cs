// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Cryptocurrencies;

/// <summary>
/// Encodes or decodes an integer with <see href="https://en.bitcoin.it/wiki/Protocol_documentation#Variable_length_integer">the "compact size" encoding used by Bitcoin</see>.
/// </summary>
public static class CompactSize
{
	/// <summary>
	/// Gets the number of bytes required to encode an integer.
	/// </summary>
	/// <param name="value">The value to be encoded.</param>
	/// <returns>The number of bytes required.</returns>
	public static int GetEncodedLength(ulong value)
	{
		return value switch
		{
			< 0xfd => 1,
			<= 0xffff => 3,
			<= 0xffffffff => 5,
			_ => 9,
		};
	}

	/// <summary>
	/// Encodes an integer.
	/// </summary>
	/// <param name="value">The integer to encode.</param>
	/// <param name="buffer">The buffer to receive the encoding. This should be at least 9 bytes to store the largest integers.</param>
	/// <returns>The number of bytes actually written to <paramref name="buffer"/>.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="buffer"/> is too small.</exception>
	public static int Encode(ulong value, Span<byte> buffer)
	{
		try
		{
			if (value < 0xfd)
			{
				buffer[0] = (byte)value;
				return 1;
			}
			else if (value <= 0xffff)
			{
				buffer[0] = 0xfd;
				buffer[1] = (byte)value;
				buffer[2] = (byte)(value >> 8);
				return 3;
			}
			else if (value <= 0xffffffff)
			{
				buffer[0] = 0xfe;
				buffer[1] = (byte)value;
				buffer[2] = (byte)(value >> 8);
				buffer[3] = (byte)(value >> 16);
				buffer[4] = (byte)(value >> 24);
				return 5;
			}
			else
			{
				buffer[0] = 0xff;
				buffer[1] = (byte)value;
				buffer[2] = (byte)(value >> 8);
				buffer[3] = (byte)(value >> 16);
				buffer[4] = (byte)(value >> 24);
				buffer[5] = (byte)(value >> 32);
				buffer[6] = (byte)(value >> 40);
				buffer[7] = (byte)(value >> 48);
				buffer[8] = (byte)(value >> 56);
				return 9;
			}
		}
		catch (IndexOutOfRangeException e)
		{
			throw new ArgumentException("Buffer too small.", e);
		}
	}

	/// <summary>
	/// Decodes an integer.
	/// </summary>
	/// <param name="buffer">The buffer to decode from.</param>
	/// <param name="value">Receives the decoded integer.</param>
	/// <returns>The number of bytes actually read.</returns>
	public static int Decode(ReadOnlySpan<byte> buffer, out ulong value)
	{
		switch (buffer[0])
		{
			case < 0xfd:
				value = buffer[0];
				return 1;
			case 0xfd:
				value = buffer[1] | ((ulong)buffer[2] << 8);
				return 3;
			case 0xfe:
				value = buffer[1] | ((ulong)buffer[2] << 8) | ((ulong)buffer[3] << 16) | ((ulong)buffer[4] << 24);
				return 5;
			case 0xff:
				value = buffer[1] | ((ulong)buffer[2] << 8) | ((ulong)buffer[3] << 16) | ((ulong)buffer[4] << 24) | ((ulong)buffer[5] << 32) | ((ulong)buffer[6] << 40) | ((ulong)buffer[7] << 48) | ((ulong)buffer[8] << 56);
				return 9;
		}
	}
}
