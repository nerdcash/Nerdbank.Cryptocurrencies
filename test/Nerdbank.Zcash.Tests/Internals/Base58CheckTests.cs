// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Internals;

public class Base58CheckTests
{
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
        Span<char> encodedChars = stackalloc char[50];
        int actualCount = Base58Check.Encode(decodedBytes, encodedChars);
        Assert.Equal(base58checkEncoding, encodedChars.Slice(0, actualCount).ToString(), ignoreCase: false);
    }

    [Theory, MemberData(nameof(Pairings))]
    public void Decode(string hexEncoding, string base58checkEncoding)
    {
        int expectedBytesWrittenCount = hexEncoding.Length / 2;

        Span<byte> actual = stackalloc byte[100];
        int actualBytesWrittenCount = Base58Check.Decode(base58checkEncoding, actual);

        Assert.Equal(expectedBytesWrittenCount, actualBytesWrittenCount);
        Assert.Equal(hexEncoding, Convert.ToHexString(actual.Slice(0, actualBytesWrittenCount)), ignoreCase: true);
    }

    [Theory]
    [InlineData("")]
    [InlineData("111")]
    public void Decode_BadInput(string badBase58)
    {
        Assert.Throws<FormatException>(() => Base58Check.Decode(badBase58, default));
    }

    [Fact]
    public void Decode_InputBufferTooSmall()
    {
        Assert.Throws<ArgumentException>(() =>
        {
            Span<byte> bytes = stackalloc byte[10];
            Base58Check.Decode("1PMycacnJaSqwwJqjawXBErnLsZ7RkXUAs", bytes);
        });
    }
}
