// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Cryptocurrencies;

/// <summary>
/// Decodes binary data into its raw primitives.
/// </summary>
public struct DecodingReader
{
	private ReadOnlyMemory<byte> buffer;

	/// <summary>
	/// Initializes a new instance of the <see cref="DecodingReader"/> struct.
	/// </summary>
	/// <param name="buffer">The buffer to decode.</param>
	public DecodingReader(ReadOnlyMemory<byte> buffer)
	{
		this.buffer = buffer;
	}

	/// <summary>
	/// Gets the number of bytes remaining to be read.
	/// </summary>
	public int RemainingLength => this.buffer.Length;

	/// <summary>
	/// Reads a little-endian encoded <see cref="uint"/> value.
	/// </summary>
	/// <returns>The decoded value.</returns>
	public uint ReadUInt32LE()
	{
		uint result = BitUtilities.ReadUInt32LE(this.buffer.Span);
		this.buffer = this.buffer[sizeof(uint)..];
		return result;
	}

	/// <summary>
	/// Reads a little-endian encoded <see cref="ulong"/> value.
	/// </summary>
	/// <returns>The decoded value.</returns>
	public ulong ReadUInt64LE()
	{
		ulong result = BitUtilities.ReadUInt64LE(this.buffer.Span);
		this.buffer = this.buffer[sizeof(ulong)..];
		return result;
	}

	/// <summary>
	/// Reads a little-endian encoded <see cref="long"/> value.
	/// </summary>
	/// <returns>The decoded value.</returns>
	public long ReadInt64LE()
	{
		long result;
		if (BitConverter.IsLittleEndian)
		{
			result = BitConverter.ToInt64(this.buffer.Span);
		}
		else
		{
			throw new NotImplementedException();
		}

		this.buffer = this.buffer[sizeof(long)..];
		return result;
	}

	/// <summary>
	/// Fills a given buffer with the next block of encoded data and advances the reader.
	/// </summary>
	/// <param name="buffer">The buffer to fill.</param>
	public void Read(Span<byte> buffer)
	{
		this.buffer[..buffer.Length].Span.CopyTo(buffer);
		this.buffer = this.buffer[buffer.Length..];
	}

	/// <summary>
	/// Returns a span into the underlying buffer with the specified length, and advances the reader.
	/// </summary>
	/// <param name="length">The length of the span to return.</param>
	/// <returns>The span into the encoded bytes.</returns>
	public ReadOnlyMemory<byte> Read(int length)
	{
		ReadOnlyMemory<byte> result = this.buffer[..length];
		this.buffer = this.buffer[length..];
		return result;
	}

	/// <summary>
	/// Reads a <see cref="CompactSize"/>-encoded <see cref="ulong"/>.
	/// </summary>
	/// <returns>The decoded value.</returns>
	public ulong ReadUInt64Compact()
	{
		int bytesRead = CompactSize.Decode(this.buffer.Span, out ulong result);
		this.buffer = this.buffer[bytesRead..];
		return result;
	}

	/// <summary>
	/// Reads a <see cref="CompactSize" />-encoded <see cref="ulong"/>.
	/// </summary>
	/// <returns>The decoded value, truncated to what an <see cref="int"/> can represent.</returns>
	/// <exception cref="OverflowException">Thrown if the decoded value exceeds what an <see cref="int"/> can represent.</exception>
	public int ReadInt32Compact() => checked((int)this.ReadUInt64Compact());

	/// <summary>
	/// Reads a byte.
	/// </summary>
	/// <returns>The byte.</returns>
	public byte ReadByte()
	{
		byte result = this.buffer.Span[0];
		this.buffer = this.buffer[1..];
		return result;
	}
}
