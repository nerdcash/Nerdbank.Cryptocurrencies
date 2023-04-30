// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Encoder = Nerdbank.Cryptocurrencies.Base58Check;

public class Base58CheckTests
{
	private readonly ITestOutputHelper logger;

	public Base58CheckTests(ITestOutputHelper logger)
	{
		this.logger = logger;
	}

	/// <summary>
	/// Gets the hex encoded and base58check encoded equivalents.
	/// </summary>
	public static object?[][] Pairings => new object?[][]
	{
		new[] { "00F54A5851E9372B87810A8E60CDD2E7CFD80B6E31", "1PMycacnJaSqwwJqjawXBErnLsZ7RkXUAs" },
		new[] { string.Empty, "3QJmnh" },
	};

	[Theory, MemberData(nameof(Pairings))]
	public void Encode(string hexEncoding, string base58checkEncoding)
	{
		Span<byte> decodedBytes = Convert.FromHexString(hexEncoding);
		Span<char> encodedChars = stackalloc char[Encoder.GetMaxEncodedLength(decodedBytes.Length)];
		int actualCount = Encoder.Encode(decodedBytes, encodedChars);
		Assert.Equal(base58checkEncoding, encodedChars[..actualCount].ToString(), ignoreCase: false);
	}

	[Theory, MemberData(nameof(Pairings))]
	public void Decode(string hexEncoding, string base58checkEncoding)
	{
		int expectedBytesWrittenCount = hexEncoding.Length / 2;

		Span<byte> actual = stackalloc byte[Encoder.GetMaxDecodedLength(base58checkEncoding.Length)];
		int actualBytesWrittenCount = Encoder.Decode(base58checkEncoding, actual);

		Assert.Equal(expectedBytesWrittenCount, actualBytesWrittenCount);
		Assert.Equal(hexEncoding, Convert.ToHexString(actual[..actualBytesWrittenCount]), ignoreCase: true);
	}

	[Theory]
	[InlineData("")]
	[InlineData("111")]
	public void Decode_BadInput(string badBase58)
	{
		Assert.Throws<FormatException>(() => Encoder.Decode(badBase58, default));
	}

	[Fact]
	public void Decode_InputBufferTooSmall()
	{
		Assert.Throws<ArgumentException>(() =>
		{
			Span<byte> bytes = stackalloc byte[10];
			Encoder.Decode("1PMycacnJaSqwwJqjawXBErnLsZ7RkXUAs", bytes);
		});
	}

	[Fact]
	public void TryDecode_InvalidChecksum()
	{
		// Arrange
		var encoded = "qwerty";
		var bytes = new byte[10];
		DecodeError expectedDecodeResult = DecodeError.InvalidChecksum;

		// Act
		bool result = Base58Check.TryDecode(encoded.AsSpan(), bytes, out DecodeError? decodeResult, out _, out _);

		// Assert
		Assert.False(result);
		Assert.Equal(expectedDecodeResult, decodeResult);
	}

	[Fact]
	public void TryDecode_InputTooShort_InvalidChecksumResult()
	{
		// Arrange
		var encoded = "StV1DL6CwTryKyV";
		var bytes = new byte[10];
		DecodeError expectedDecodeResult = DecodeError.InvalidChecksum;

		// Act
		bool result = Base58Check.TryDecode(encoded.AsSpan(), bytes, out DecodeError? decodeResult, out _, out _);

		// Assert
		Assert.False(result);
		Assert.Equal(expectedDecodeResult, decodeResult);
	}

	[Fact]
	public void TryDecode_InvalidCharacter()
	{
		Assert.False(Base58Check.TryDecode(":", new byte[10], out DecodeError? decodeError, out string? errorMessage, out _));
		this.logger.WriteLine(errorMessage);
		Assert.Equal(DecodeError.InvalidCharacter, decodeError.Value);
	}

	[Fact]
	public void TryDecode_OutputBufferTooShort()
	{
		Assert.False(Base58Check.TryDecode("1PMycacnJaSqwwJqjawXBErnLsZ7RkXUAs", new byte[5], out DecodeError? decodeError, out string? errorMessage, out _));
		this.logger.WriteLine(errorMessage);
		Assert.Equal(DecodeError.BufferTooSmall, decodeError.Value);
	}
}
