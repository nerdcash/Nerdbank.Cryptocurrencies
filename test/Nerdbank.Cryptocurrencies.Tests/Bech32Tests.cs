// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Utilities;

public class Bech32Tests
{
	private readonly ITestOutputHelper logger;

	public Bech32Tests(ITestOutputHelper logger)
	{
		this.logger = logger;
	}

	// TODO: Add invalid test cases.

	/// <summary>
	/// Gets the hex encoded and Bech32 encoded equivalents.
	/// </summary>
	/// <remarks>
	/// A <see href="https://slowli.github.io/bech32-buffer/">useful test case generator</see>,
	/// configured to its Data tab and with Bech32 selected, was used to generate some of these test cases.
	/// </remarks>
	public static object?[][] Bech32Pairings => new object?[][]
	{
		new object?[] { ("my", "010203"), "my1qypqxsvr6ys" },
		new object?[] { ("my", "abcd"), "my140xshf6d6q" },
		new object?[] { ("my", "ab"), "my14vf0uuar" },
		new object?[] { ("my", string.Empty), "my1h5ufw5" },
		new object?[] { ("sometag", "abcdef0110ffeedd"), "sometag140x77qgsllhd6qcua60" },
		new object?[] { ("sometag", "abcdef0110ffeeddc0ffee"), "sometag140x77qgsllhdms8lacykfwm3" },
		new object?[] { ("somet1ag", "abcdef0110ffeedd"), "somet1ag140x77qgsllhd6sjr0yn" },
	};

	/// <summary>
	/// Gets the hex encoded and Bech32m encoded equivalents.
	/// </summary>
	public static object?[][] Bech32mPairings => new object?[][]
	{
		new object?[] { ("split", "c5f38b70305f519bf66d85fb6cf03058f3dde463ecd7918f2dc743918f2d"), "split1checkupstagehandshakeupstreamerranterredcaperredlc445v" },
		new object?[] { ("sometag", "abcdef0110ffeedd"), "sometag140x77qgsllhd64yv3ld" },
		new object?[] { ("an83characterlonghumanreadablepartthatcontainsthetheexcludedcharactersbioandnumber1", string.Empty), "an83characterlonghumanreadablepartthatcontainsthetheexcludedcharactersbioandnumber11sg7hg6" },
		new object?[] { ("?", string.Empty), "?1v759aa" },
		new object?[] { ("a", string.Empty), "A1LQFN3A" },
		new object?[] { ("a", string.Empty), "a1lqfn3a" },
	};

	[Theory, MemberData(nameof(Bech32Pairings))]
	public void Encode_Bech32((string Tag, string HexData) human, string bech32Encoding)
	{
		Span<byte> data = Convert.FromHexString(human.HexData);
		Span<char> encodedChars = stackalloc char[Bech32.GetEncodedLength(human.Tag.Length, data.Length)];
		int actualCount = Bech32.Original.Encode(human.Tag, data, encodedChars);
		Assert.Equal(bech32Encoding, encodedChars[..actualCount].ToString(), ignoreCase: false);
	}

	[Theory, MemberData(nameof(Bech32Pairings))]
	public void Decode_Bech32((string Tag, string HexData) human, string bech32Encoding)
	{
		(int TagLength, int DataLength)? maxLength = Bech32.GetDecodedLength(bech32Encoding);
		Assert.NotNull(maxLength);
		Span<char> tag = stackalloc char[maxLength.Value.TagLength];
		Span<byte> data = stackalloc byte[maxLength.Value.DataLength];
		(int tagLength, int dataLength) = Bech32.Original.Decode(bech32Encoding, tag, data);

		Assert.Equal(human.Tag, tag[..tagLength].ToString(), ignoreCase: false);
		Assert.Equal(human.HexData, Convert.ToHexString(data[..dataLength]).ToLowerInvariant(), ignoreCase: true);
	}

	[Theory, MemberData(nameof(Bech32mPairings))]
	public void Encode_Bech32m((string Tag, string HexData) human, string bech32Encoding)
	{
		Span<byte> data = Convert.FromHexString(human.HexData);
		Span<char> encodedChars = stackalloc char[Bech32.GetEncodedLength(human.Tag.Length, data.Length)];
		int actualCount = Bech32.Bech32m.Encode(human.Tag, data, encodedChars);
		Assert.Equal(bech32Encoding.ToLowerInvariant(), encodedChars[..actualCount].ToString(), ignoreCase: false);
	}

	[Theory, MemberData(nameof(Bech32mPairings))]
	public void Decode_Bech32m((string Tag, string HexData) human, string bech32Encoding)
	{
		(int TagLength, int DataLength)? maxLength = Bech32.GetDecodedLength(bech32Encoding);
		Assert.NotNull(maxLength);
		Span<char> tag = stackalloc char[maxLength.Value.TagLength];
		Span<byte> data = stackalloc byte[maxLength.Value.DataLength];
		(int tagLength, int dataLength) = Bech32.Bech32m.Decode(bech32Encoding, tag, data);

		Assert.Equal(human.Tag, tag[..tagLength].ToString(), ignoreCase: false);
		Assert.Equal(human.HexData, Convert.ToHexString(data[..dataLength]).ToLowerInvariant(), ignoreCase: true);
	}

	[Theory, MemberData(nameof(Bech32Pairings))]
	public void GetDecodedLength((string Tag, string HexData) human, string bech32Encoding)
	{
		(int TagLength, int DataLength)? maxLength = Bech32.GetDecodedLength(bech32Encoding);
		Assert.NotNull(maxLength);
		Assert.Equal(human.Tag.Length, maxLength.Value.TagLength);
		Assert.Equal(human.HexData.Length / 2, maxLength.Value.DataLength);
	}

	[Fact]
	public void GetDecodedLength_NoSeparator()
	{
		Assert.Null(Bech32.GetDecodedLength("234"));
	}

	[Fact]
	public void GetDecodedLength_NoData()
	{
		Assert.Null(Bech32.GetDecodedLength("u1"));
	}

	[Fact]
	public void TryDecode_NoSeparator()
	{
		Span<char> tag = stackalloc char[10];
		Span<byte> data = stackalloc byte[10];
		Assert.False(Bech32.Original.TryDecode("234", tag, data, out DecodeError? error, out string? msg, out (int Tag, int Data) length));
		Assert.Equal(DecodeError.NoSeparator, error);
		this.logger.WriteLine(msg);
	}

	[Fact]
	public void TryDecode_TagBufferTooSmall_ReturnsFalse()
	{
		// Arrange
		var encoded = "teststring1q8bchz";
		var tag = new char[5];
		var data = new byte[10];

		// Act
		bool result = Bech32.Original.TryDecode(encoded, tag, data, out DecodeError? decodeResult, out _, out _);

		// Assert
		Assert.False(result);
		Assert.Equal(DecodeError.BufferTooSmall, decodeResult);
	}

	[Fact]
	public void TryDecode_InvalidChecksum()
	{
		Span<char> tag = stackalloc char[10];
		Span<byte> data = stackalloc byte[10];
		Assert.False(Bech32.Original.TryDecode("my14vf0uuur", tag, data, out DecodeError? error, out string? msg, out (int Tag, int Data) length));
		Assert.Equal(DecodeError.InvalidChecksum, error);
		this.logger.WriteLine(msg);
	}

	[Fact]
	public void TryDecode_ViolatesAlphabet()
	{
		Span<char> tag = stackalloc char[10];
		Span<byte> data = stackalloc byte[10];
		Assert.False(Bech32.Original.TryDecode("my14vf0uubr", tag, data, out DecodeError? error, out string? msg, out (int Tag, int Data) length));
		Assert.Equal(DecodeError.InvalidCharacter, error);
		this.logger.WriteLine(msg);
	}

	[Theory, MemberData(nameof(Bech32Pairings))]
	public void GetEncodedLength((string Tag, string HexData) human, string bech32Encoding)
	{
		int length = Bech32.GetEncodedLength(human.Tag.Length, human.HexData.Length / 2);
		Assert.Equal(bech32Encoding.Length, length);
	}

	[Fact]
	public void Decode_InputBufferTooSmall()
	{
		Assert.Throws<ArgumentException>(() =>
		{
			Span<char> tinyChars = stackalloc char[1];
			Span<byte> largeBytes = stackalloc byte[100];
			Bech32.Original.Decode((string)Bech32Pairings[0][1]!, tinyChars, largeBytes);
		});
		Assert.Throws<ArgumentException>(() =>
		{
			Span<char> largeChars = stackalloc char[100];
			Span<byte> tinyBytes = stackalloc byte[1];
			Bech32.Original.Decode((string)Bech32Pairings[0][1]!, largeChars, tinyBytes);
		});
	}
}
