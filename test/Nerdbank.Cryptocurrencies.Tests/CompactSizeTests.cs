// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Encoder = Nerdbank.Cryptocurrencies.CompactSize;

namespace Utilities;

public class CompactSizeTests
{
	/// <summary>
	/// Gets the hex encoded and base58check encoded equivalents.
	/// </summary>
	public static object?[][] Pairings => new object?[][]
	{
		new object?[] { 0UL, "00" },
		new object?[] { 5UL, "05" },
		new object?[] { 0xffffUL, "FDffff" },
		new object?[] { 0xffffffffUL, "FEffffffff" },
		new object?[] { 0xffffffffffffffffUL, "FFffffffffffffffff" },
	};

	[Theory, MemberData(nameof(Pairings))]
	public void Encode(ulong value, string hex)
	{
		Span<byte> encodedBytes = stackalloc byte[9];
		int actualCount = Encoder.Encode(value, encodedBytes);
		Assert.Equal(hex, Convert.ToHexString(encodedBytes.Slice(0, actualCount)), ignoreCase: true);
	}

	[Theory, MemberData(nameof(Pairings))]
	public void Decode(ulong value, string hex)
	{
		Span<byte> encodedBytes = Convert.FromHexString(hex);
		int actualBytesWrittenCount = Encoder.Decode(encodedBytes, out ulong actualValue);
		Assert.Equal(encodedBytes.Length, actualBytesWrittenCount);
		Assert.Equal(value, actualValue);
	}

	[Fact]
	public void Decode_InputBufferTooSmall()
	{
		Assert.Throws<ArgumentException>(() =>
		{
			Span<byte> bytes = stackalloc byte[2];
			Encoder.Encode(0xffff, bytes);
		});
	}
}
