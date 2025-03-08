// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Cryptocurrencies;

/// <summary>
/// Static operations that can be performed bitwise on integers.
/// </summary>
public static class BitUtilities
{
	/// <summary>
	/// Writes a little-endian 32-bit integer to the given buffer.
	/// </summary>
	/// <param name="value">The value to write.</param>
	/// <param name="buffer">The buffer.</param>
	/// <returns>The number of bytes written to <paramref name="buffer"/> (always 4).</returns>
	public static int WriteLE(uint value, Span<byte> buffer)
	{
		if (BitConverter.IsLittleEndian)
		{
			if (!BitConverter.TryWriteBytes(buffer, value))
			{
				throw new ArgumentException(Strings.TargetBufferTooSmall);
			}
		}
		else
		{
			buffer[3] = (byte)(value >> (3 * 8));
			buffer[2] = (byte)(value >> (2 * 8));
			buffer[1] = (byte)(value >> (1 * 8));
			buffer[0] = (byte)value;
		}

		return sizeof(uint);
	}

	/// <summary>
	/// Writes a big-endian 32-bit integer to the given buffer.
	/// </summary>
	/// <param name="value">The value to write.</param>
	/// <param name="buffer">The buffer.</param>
	/// <returns>The number of bytes written to <paramref name="buffer"/> (always 4).</returns>
	public static int WriteBE(uint value, Span<byte> buffer)
	{
		if (!BitConverter.IsLittleEndian)
		{
			if (!BitConverter.TryWriteBytes(buffer, value))
			{
				throw new ArgumentException(Strings.TargetBufferTooSmall);
			}
		}
		else
		{
			buffer[3] = (byte)value;
			buffer[2] = (byte)(value >> (1 * 8));
			buffer[1] = (byte)(value >> (2 * 8));
			buffer[0] = (byte)(value >> (3 * 8));
		}

		return sizeof(uint);
	}

	/// <summary>
	/// Reads a big-endian 32-bit integer from the given buffer.
	/// </summary>
	/// <param name="buffer">The buffer to read from.</param>
	/// <returns>The unsigned 32-bit integer.</returns>
	public static uint ReadUInt32BE(ReadOnlySpan<byte> buffer)
	{
		if (!BitConverter.IsLittleEndian)
		{
			return BitConverter.ToUInt32(buffer);
		}
		else
		{
			return
				buffer[3] |
				((uint)buffer[2] << (1 * 8)) |
				((uint)buffer[1] << (2 * 8)) |
				((uint)buffer[0] << (3 * 8));
		}
	}

	/// <summary>
	/// Reads a little-endian 32-bit integer from the given buffer.
	/// </summary>
	/// <param name="buffer">The buffer to read from.</param>
	/// <returns>The unsigned 32-bit integer.</returns>
	public static uint ReadUInt32LE(ReadOnlySpan<byte> buffer)
	{
		if (BitConverter.IsLittleEndian)
		{
			return BitConverter.ToUInt32(buffer);
		}
		else
		{
			return
				((uint)buffer[3] << (3 * 8)) |
				((uint)buffer[2] << (2 * 8)) |
				((uint)buffer[1] << (1 * 8)) |
				buffer[0];
		}
	}

	/// <summary>
	/// Reads a little-endian 64-bit integer from the given buffer.
	/// </summary>
	/// <param name="buffer">The buffer to read from.</param>
	/// <returns>The unsigned 64-bit integer.</returns>
	public static ulong ReadUInt64LE(ReadOnlySpan<byte> buffer)
	{
		if (BitConverter.IsLittleEndian)
		{
			return BitConverter.ToUInt64(buffer);
		}
		else
		{
			return
				((ulong)buffer[7] << (7 * 8)) |
				((ulong)buffer[6] << (6 * 8)) |
				((ulong)buffer[5] << (5 * 8)) |
				((ulong)buffer[4] << (4 * 8)) |
				((ulong)buffer[3] << (3 * 8)) |
				((ulong)buffer[2] << (2 * 8)) |
				((ulong)buffer[1] << (1 * 8)) |
				buffer[0];
		}
	}

	/// <summary>
	/// Writes a little-endian 64-bit integer to the given buffer.
	/// </summary>
	/// <param name="value">The value to write.</param>
	/// <param name="buffer">The buffer.</param>
	/// <returns>The number of bytes written to <paramref name="buffer"/> (always 8).</returns>
	public static int WriteLE(ulong value, Span<byte> buffer)
	{
		if (BitConverter.IsLittleEndian)
		{
			if (!BitConverter.TryWriteBytes(buffer, value))
			{
				throw new ArgumentException(Strings.TargetBufferTooSmall);
			}
		}
		else
		{
			buffer[7] = (byte)(value >> (7 * 8));
			buffer[6] = (byte)(value >> (6 * 8));
			buffer[5] = (byte)(value >> (5 * 8));
			buffer[4] = (byte)(value >> (4 * 8));
			buffer[3] = (byte)(value >> (3 * 8));
			buffer[2] = (byte)(value >> (2 * 8));
			buffer[1] = (byte)(value >> (1 * 8));
			buffer[0] = (byte)value;
		}

		return sizeof(ulong);
	}
}
