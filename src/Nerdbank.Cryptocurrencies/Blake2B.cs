// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/*
 * The source code originated from https://github.com/BLAKE2/BLAKE2/tree/2407e7a40a2650872c5a2100498960662ea22464/csharp/Blake2Sharp
 * It was written in 2012 by Christian Winnerlein  <codesinchaos@gmail.com>
 * and originally licensed under Creative Commons Zero v1.0 Universal: https://github.com/BLAKE2/BLAKE2/blob/2407e7a40a2650872c5a2100498960662ea22464/COPYING
 * It was later substantially modified by Andrew Arnott as part of the containing project and a new license and copyright applied.
 */

using System.Runtime.InteropServices;

namespace Nerdbank.Cryptocurrencies;

/// <summary>
/// The Blake2B hashing algorithm.
/// </summary>
public class Blake2B
{
	private const int NumberOfRounds = 12;
	private const int BlockSizeInBytes = 128;

	private const ulong IV0 = 0x6A09E667F3BCC908UL;
	private const ulong IV1 = 0xBB67AE8584CAA73BUL;
	private const ulong IV2 = 0x3C6EF372FE94F82BUL;
	private const ulong IV3 = 0xA54FF53A5F1D36F1UL;
	private const ulong IV4 = 0x510E527FADE682D1UL;
	private const ulong IV5 = 0x9B05688C2B3E6C1FUL;
	private const ulong IV6 = 0x1F83D9ABFB41BD6BUL;
	private const ulong IV7 = 0x5BE0CD19137E2179UL;

	private static readonly ThreadLocal<Blake2B> RecycleBlake = new();

	private static readonly int[] Sigma = new int[NumberOfRounds * 16]
	{
		0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15,
		14, 10, 4, 8, 9, 15, 13, 6, 1, 12, 0, 2, 11, 7, 5, 3,
		11, 8, 12, 0, 5, 2, 15, 13, 10, 14, 3, 6, 7, 1, 9, 4,
		7, 9, 3, 1, 13, 12, 11, 14, 2, 6, 5, 10, 4, 0, 15, 8,
		9, 0, 5, 7, 2, 4, 10, 15, 14, 1, 11, 12, 6, 8, 3, 13,
		2, 12, 6, 10, 0, 11, 8, 3, 4, 13, 7, 5, 15, 14, 1, 9,
		12, 5, 1, 15, 14, 13, 4, 10, 0, 7, 6, 3, 9, 2, 8, 11,
		13, 11, 7, 14, 12, 1, 3, 9, 5, 0, 15, 4, 8, 6, 2, 10,
		6, 15, 14, 9, 11, 3, 0, 8, 12, 2, 13, 7, 1, 4, 10, 5,
		10, 2, 8, 4, 7, 6, 1, 5, 15, 11, 9, 14, 3, 12, 13, 0,
		0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15,
		14, 10, 4, 8, 9, 15, 13, 6, 1, 12, 0, 2, 11, 7, 5, 3,
	};

	private Buffers buffers;

	private bool isInitialized;

	private int bufferFilled;

	private ulong counter0;
	private ulong counter1;
	private ulong finalizationFlag0;
	private ulong finalizationFlag1;

	/// <summary>
	/// Initializes a new instance of the <see cref="Blake2B"/> class.
	/// </summary>
	public Blake2B()
		: this(new())
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="Blake2B"/> class.
	/// </summary>
	/// <param name="config">Configuration parameters for the hash function.</param>
	public Blake2B(in Config config)
	{
		this.Reset(config);
	}

	/// <summary>
	/// Gets the size of the hash output in bytes.
	/// </summary>
	/// <remarks>
	/// This value can be configured via the <see cref="Config.OutputSizeInBytes"/> property of the <see cref="Config"/> struct passed to the constructor or the <see cref="Reset(in Config)"/> method.
	/// </remarks>
	public int OutputSizeInBytes { get; private set; }

	/// <inheritdoc cref="ComputeHash(ReadOnlySpan{byte}, Span{byte}, in Config)"/>
	public static int ComputeHash(ReadOnlySpan<byte> data, Span<byte> hash) => ComputeHash(data, hash, new Config());

	/// <summary>
	/// Computes the Blake2B hash of the specified data.
	/// </summary>
	/// <param name="data">The data to hash.</param>
	/// <param name="hash">Receives the hash of the <paramref name="data"/>.</param>
	/// <param name="config">Configuration parameters for the hash function.</param>
	/// <returns>The number of bytes written to the <paramref name="hash"/> parameter.</returns>
	/// <remarks>
	/// This function has an amortized allocation cost of 0.
	/// </remarks>
	public static int ComputeHash(ReadOnlySpan<byte> data, Span<byte> hash, in Config config)
	{
		if (RecycleBlake.Value is Blake2B hasher)
		{
			hasher.Reset(config);
		}
		else
		{
			RecycleBlake.Value = hasher = new(config);
		}

		hasher.Update(data);
		return hasher.Finish(hash);
	}

	/// <summary>
	/// Mixes more data into the hash.
	/// </summary>
	/// <param name="data">The data to be hashed.</param>
	/// <remarks>Call <see cref="Finish(Span{byte})"/> when done submitting data to obtain the final hash.</remarks>
	public void Update(ReadOnlySpan<byte> data) => this.HashCore(data);

	/// <summary>
	/// Obtains the final hash and transitions this object to an uninitialized state.
	/// </summary>
	/// <param name="finalHash">Receives the final hash. This buffer must be at least <see cref="OutputSizeInBytes"/> in length.</param>
	/// <returns>The number of bytes written to <paramref name="finalHash"/>.</returns>
	/// <exception cref="InvalidOperationException">Thrown if this method has been called more recently than the last call to <see cref="Reset()"/>.</exception>
	/// <remarks>
	/// Use <see cref="Reset()"/> to re-initialize this object for another hash computation.
	/// </remarks>
	public int Finish(Span<byte> finalHash)
	{
		if (!this.isInitialized)
		{
			throw new InvalidOperationException(Strings.FormatCallResetFirst(nameof(this.Reset)));
		}

		this.isInitialized = false;

		// Last compression
		this.counter0 += (uint)this.bufferFilled;
		this.finalizationFlag0 = ulong.MaxValue;

		Span<byte> buf = this.buffers.Buf;
		for (int i = this.bufferFilled; i < buf.Length; i++)
		{
			buf[i] = 0;
		}

		this.Compress(buf);

		// Output
		Span<byte> hash = stackalloc byte[64];
		Span<ulong> h = this.buffers.H;
		for (int i = 0; i < 8; ++i)
		{
			WriteInt64LE(h[i], hash[(i << 3)..]);
		}

		int bytesWritten = Math.Min(finalHash.Length, hash.Length);
		hash[..bytesWritten].CopyTo(finalHash);
		return bytesWritten;
	}

	/// <inheritdoc cref="Reset(in Config)"/>
	public void Reset() => this.Reset(new Config());

	/// <summary>
	/// Clears all state on this object in preparation for computing another hash.
	/// </summary>
	/// <param name="config">Configuration parameters for the hash function.</param>
	public void Reset(in Config config)
	{
		this.buffers = default;

		this.OutputSizeInBytes = config.OutputSizeInBytes;

		ApplyConfig(config, this.buffers.RawConfig);
		if (!config.Key.IsEmpty)
		{
			this.buffers.IsKeySet = true;
			config.Key.CopyTo(this.buffers.Key);
		}

		this.isInitialized = true;

		Span<ulong> h = this.buffers.H;

		h[0] = IV0;
		h[1] = IV1;
		h[2] = IV2;
		h[3] = IV3;
		h[4] = IV4;
		h[5] = IV5;
		h[6] = IV6;
		h[7] = IV7;

		this.counter0 = 0;
		this.counter1 = 0;
		this.finalizationFlag0 = 0;
		this.finalizationFlag1 = 0;

		this.bufferFilled = 0;

		this.buffers.Buf.Clear();

		Span<ulong> rawConfig = this.buffers.RawConfig;
		for (int i = 0; i < 8; i++)
		{
			h[i] ^= rawConfig[i];
		}

		if (this.buffers.IsKeySet)
		{
			this.HashCore(this.buffers.Key);
		}
	}

	private static void ApplyConfig(in Config config, Span<ulong> rawConfig)
	{
		int intermediateHashSize = 0;
		int leafSize = 0;
		int fanOut = 1;
		int maxHeight = 1;

		// digest length
		if (config.OutputSizeInBytes <= 0 || config.OutputSizeInBytes > 64)
		{
			throw new ArgumentOutOfRangeException("config.OutputSize");
		}

		rawConfig[0] |= (uint)config.OutputSizeInBytes;

		// Key length
		if (!config.Key.IsEmpty)
		{
			if (config.Key.Length > 64)
			{
				throw new ArgumentException("config.Key", "Key too long");
			}

			rawConfig[0] |= (uint)config.Key.Length << 8;
		}

		// FanOut
		rawConfig[0] |= (uint)fanOut << 16;

		// Depth
		rawConfig[0] |= (uint)maxHeight << 24;

		// Leaf length
		rawConfig[0] |= ((ulong)(uint)leafSize) << 32;

		rawConfig[2] |= (uint)intermediateHashSize << 8;

		// Salt
		if (!config.Salt.IsEmpty)
		{
			if (config.Salt.Length != 16)
			{
				throw new ArgumentException("config.Salt has invalid length");
			}

			rawConfig[4] = ReadUInt64LE(config.Salt);
			rawConfig[5] = ReadUInt64LE(config.Salt[8..]);
		}

		// Personalization
		if (!config.Personalization.IsEmpty)
		{
			if (config.Personalization.Length != 16)
			{
				throw new ArgumentException("config.Personalization has invalid length");
			}

			rawConfig[6] = ReadUInt64LE(config.Personalization);
			rawConfig[7] = ReadUInt64LE(config.Personalization[8..]);
		}
	}

	/// <summary>
	/// Reads a little-endian 64-bit integer from the given buffer.
	/// </summary>
	/// <param name="buffer">The buffer to read from.</param>
	/// <returns>The unsigned 64-bit integer.</returns>
	private static ulong ReadUInt64LE(ReadOnlySpan<byte> buffer)
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
	private static void WriteInt64LE(ulong value, Span<byte> buffer)
	{
		if (BitConverter.IsLittleEndian)
		{
			if (!BitConverter.TryWriteBytes(buffer, value))
			{
				throw new IndexOutOfRangeException();
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
	}

	private void HashCore(ReadOnlySpan<byte> array)
	{
		if (!this.isInitialized)
		{
			throw new InvalidOperationException(Strings.FormatCallResetFirst(nameof(this.Reset)));
		}

		int offset = 0;
		int count = array.Length;
		int bufferRemaining = BlockSizeInBytes - this.bufferFilled;

		if ((this.bufferFilled > 0) && (count > bufferRemaining))
		{
			array.Slice(offset, bufferRemaining).CopyTo(this.buffers.Buf[this.bufferFilled..]);
			this.counter0 += BlockSizeInBytes;
			if (this.counter0 == 0)
			{
				this.counter1++;
			}

			this.Compress(this.buffers.Buf);
			offset += bufferRemaining;
			count -= bufferRemaining;
			this.bufferFilled = 0;
		}

		while (count > BlockSizeInBytes)
		{
			this.counter0 += BlockSizeInBytes;
			if (this.counter0 == 0)
			{
				this.counter1++;
			}

			this.Compress(array[offset..]);
			offset += BlockSizeInBytes;
			count -= BlockSizeInBytes;
		}

		if (count > 0)
		{
			array.Slice(offset, count).CopyTo(this.buffers.Buf[this.bufferFilled..]);
			this.bufferFilled += count;
		}
	}

	private void Compress(ReadOnlySpan<byte> block)
	{
		Span<ulong> v = this.buffers.V;
		Span<ulong> h = this.buffers.H;
		Span<ulong> m = this.buffers.M;

		for (int i = 0; i < 16; ++i)
		{
			m[i] = ReadUInt64LE(block[(i << 3)..]);
		}

		v[0] = h[0];
		v[1] = h[1];
		v[2] = h[2];
		v[3] = h[3];
		v[4] = h[4];
		v[5] = h[5];
		v[6] = h[6];
		v[7] = h[7];

		v[8] = IV0;
		v[9] = IV1;
		v[10] = IV2;
		v[11] = IV3;
		v[12] = IV4 ^ this.counter0;
		v[13] = IV5 ^ this.counter1;
		v[14] = IV6 ^ this.finalizationFlag0;
		v[15] = IV7 ^ this.finalizationFlag1;

		for (int r = 0; r < NumberOfRounds; ++r)
		{
			G(0, 4, 8, 12, r, 0, v, m);
			G(1, 5, 9, 13, r, 2, v, m);
			G(2, 6, 10, 14, r, 4, v, m);
			G(3, 7, 11, 15, r, 6, v, m);
			G(3, 4, 9, 14, r, 14, v, m);
			G(2, 7, 8, 13, r, 12, v, m);
			G(0, 5, 10, 15, r, 8, v, m);
			G(1, 6, 11, 12, r, 10, v, m);
		}

		for (int i = 0; i < 8; ++i)
		{
			h[i] ^= v[i] ^ v[i + 8];
		}

		static void G(int a, int b, int c, int d, int r, int i, Span<ulong> v, Span<ulong> m)
		{
			int p = (r << 4) + i;
			int p0 = Sigma[p];
			int p1 = Sigma[p + 1];

			v[a] += v[b] + m[p0];
			v[d] = RotateRight(v[d] ^ v[a], 32);
			v[c] += v[d];
			v[b] = RotateRight(v[b] ^ v[c], 24);
			v[a] += v[b] + m[p1];
			v[d] = RotateRight(v[d] ^ v[a], 16);
			v[c] += v[d];
			v[b] = RotateRight(v[b] ^ v[c], 63);

			static ulong RotateRight(ulong value, int nBits) => (value >> nBits) | (value << (64 - nBits));
		}
	}

	/// <summary>
	/// Configuration parameters for the hash function.
	/// </summary>
	public readonly ref struct Config
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="Config"/> struct.
		/// </summary>
		public Config()
		{
		}

		/// <summary>
		/// Gets the buffer to use to personalize the hash.
		/// </summary>
		public ReadOnlySpan<byte> Personalization { get; init; }

		/// <summary>
		/// Gets the salt for the hash.
		/// </summary>
		/// <value>This must be empty or a 16-byte buffer.</value>
		public ReadOnlySpan<byte> Salt { get; init; }

		/// <summary>
		/// Gets the key for the hash.
		/// </summary>
		/// <value>This buffer must not exceed 64 bytes in length.</value>
		public ReadOnlySpan<byte> Key { get; init; }

		/// <summary>
		/// Gets the number of bytes in the output. Must be between 1 and 64, inclusive.
		/// </summary>
		public int OutputSizeInBytes { get; init; } = 64;
	}

	private unsafe struct Buffers
	{
		private fixed ulong rawConfig[8];
		private fixed ulong m[16];
		private fixed ulong h[8];
		private fixed ulong v[16];
		private fixed byte buf[128];
		private fixed byte key[128];

		internal bool IsKeySet { get; set; }

		internal Span<ulong> RawConfig => MemoryMarshal.CreateSpan(ref this.rawConfig[0], 8);

		internal Span<ulong> M => MemoryMarshal.CreateSpan(ref this.m[0], 16);

		internal Span<ulong> H => MemoryMarshal.CreateSpan(ref this.h[0], 8);

		internal Span<ulong> V => MemoryMarshal.CreateSpan(ref this.v[0], 16);

		internal Span<byte> Buf => MemoryMarshal.CreateSpan(ref this.buf[0], 128);

		internal Span<byte> Key => this.IsKeySet ? MemoryMarshal.CreateSpan(ref this.key[0], 128) : default;
	}
}
