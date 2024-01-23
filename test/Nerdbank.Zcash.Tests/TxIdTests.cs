// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public class TxIdTests
{
	private const string ExpectedString = "201f1e1d1c1b1a191817161514131211100f0e0d0c0b0a090807060504030201";
	private static readonly byte[] TxIdBytes = Enumerable.Range(1, 32).Select(v => (byte)v).ToArray();

	[Fact]
	public void ToString_HexReversedBytes()
	{
		Assert.Equal(ExpectedString, new TxId(TxIdBytes).ToString(), ignoreCase: false);
	}

	[Fact]
	public void Parse_Span()
	{
		TxId actual = TxId.Parse(ExpectedString.AsSpan());
		TxId expected = new(TxIdBytes);
		Assert.Equal(expected, actual);
	}

	[Fact]
	public void Parse_String()
	{
		TxId actual = TxId.Parse(ExpectedString);
		TxId expected = new(TxIdBytes);
		Assert.Equal(expected, actual);
		Assert.Same(ExpectedString, actual.ToString());
	}

	[Fact]
	public void Parse_BadLength()
	{
		Assert.Throws<ArgumentException>(() => TxId.Parse("201f1e1d1c1b1a191817161514131211100f0e0d0c0b0a0908070605040302011"));
	}

	[Fact]
	public void Parse_BadChars()
	{
		Assert.Throws<FormatException>(() => TxId.Parse("201g1e1d1c1b1a191817161514131211100f0e0d0c0b0a090807060504030201"));
	}

	[Fact]
	public void Parse_EmptyOrNull()
	{
		Assert.Throws<ArgumentException>(() => TxId.Parse(string.Empty));
		Assert.Throws<ArgumentException>(() => TxId.Parse(string.Empty.AsSpan()));
		Assert.Throws<ArgumentException>(() => TxId.Parse(null!));
	}

	[Fact]
	public void IsEqualDifferentBuffer()
	{
		TxId first = new(TxIdBytes);
		TxId second = new(TxIdBytes.ToArray());
		Assert.True(first.Equals(second));
		Assert.True(((IEquatable<TxId>)first).Equals(second));
		Assert.True(first.Equals((object)second));
		Assert.True(first == second);
		Assert.False(first != second);
		Assert.Equal(first.GetHashCode(), second.GetHashCode());
	}

	[Fact]
	public void NotEqual()
	{
		TxId first = new(TxIdBytes);
		TxId second = new([0xff, .. TxIdBytes[1..]]);
		Assert.False(first.Equals(second));
		Assert.False(((IEquatable<TxId>)first).Equals(second));
		Assert.False(first.Equals((object)second));
		Assert.False(first == second);
		Assert.True(first != second);
		Assert.NotEqual(first.GetHashCode(), second.GetHashCode());
	}

	[Fact]
	public void Ctor_BadLength()
	{
		Assert.Throws<ArgumentException>(() => new TxId(new byte[31]));
		Assert.Throws<ArgumentException>(() => new TxId(new byte[33]));
		Assert.Throws<ArgumentException>(() => new TxId(new byte[0]));
		Assert.Throws<ArgumentException>(() => new TxId(null!));
	}

	[Fact]
	public void Indexer_Range()
	{
		TxId txid = new(TxIdBytes);
		Assert.True(txid[..].SequenceEqual(TxIdBytes));
		Assert.True(txid[2..4].SequenceEqual(TxIdBytes[2..4]));
	}

	[Fact]
	public void ImplicitConversion_ToByteSpan()
	{
		TxId txid = new TxId(TxIdBytes);
		ReadOnlySpan<byte> txidbytes = txid;
		Assert.True(txidbytes.SequenceEqual(TxIdBytes));
	}
}
