// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Encoder = Nerdbank.Zcash.Utilities.Bech32;

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
    public static object?[][] Pairings => new object?[][]
    {
        new object?[] { ("my", "010203"), "my1qypqxsvr6ys" },
        new object?[] { ("my", "abcd"), "my140xshf6d6q" },
        new object?[] { ("my", "ab"), "my14vf0uuar" },
        new object?[] { ("my", string.Empty), "my1h5ufw5" },
        new object?[] { ("sometag", "abcdef0110ffeedd"), "sometag140x77qgsllhd6qcua60" },
        new object?[] { ("sometag", "abcdef0110ffeeddc0ffee"), "sometag140x77qgsllhdms8lacykfwm3" },
        new object?[] { ("somet1ag", "abcdef0110ffeedd"), "somet1ag140x77qgsllhd6sjr0yn" },
    };

    [Theory, MemberData(nameof(Pairings))]
    public void Encode((string Tag, string HexData) human, string bech32Encoding)
    {
        Span<byte> data = Convert.FromHexString(human.HexData);
        Span<char> encodedChars = stackalloc char[Encoder.GetEncodedLength(human.Tag.Length, data.Length)];
        int actualCount = Encoder.Encode(human.Tag, data, encodedChars);
        Assert.Equal(bech32Encoding, encodedChars.Slice(0, actualCount).ToString(), ignoreCase: false);
    }

    [Theory, MemberData(nameof(Pairings))]
    public void Decode((string Tag, string HexData) human, string bech32Encoding)
    {
        (int TagLength, int DataLength)? maxLength = Encoder.GetDecodedLength(bech32Encoding);
        Assert.NotNull(maxLength);
        Span<char> tag = stackalloc char[maxLength.Value.TagLength];
        Span<byte> data = stackalloc byte[maxLength.Value.DataLength];
        (int TagLength, int DataLength) actualLength = Encoder.Decode(bech32Encoding, tag, data);

        Assert.Equal(human.Tag, tag.Slice(0, actualLength.TagLength).ToString(), ignoreCase: false);
        Assert.Equal(human.HexData, Convert.ToHexString(data.Slice(0, actualLength.DataLength)).ToLowerInvariant(), ignoreCase: true);
    }

    [Theory, MemberData(nameof(Pairings))]
    public void GetDecodedLength((string Tag, string HexData) human, string bech32Encoding)
    {
        (int TagLength, int DataLength)? maxLength = Encoder.GetDecodedLength(bech32Encoding);
        Assert.NotNull(maxLength);
        Assert.Equal(human.Tag.Length, maxLength.Value.TagLength);
        Assert.Equal(human.HexData.Length / 2, maxLength.Value.DataLength);
    }

    [Fact]
    public void GetDecodedLength_NoSeparator()
    {
        Assert.Null(Encoder.GetDecodedLength("234"));
    }

    [Fact]
    public void TryDecode_NoSeparator()
    {
        Span<char> tag = stackalloc char[10];
        Span<byte> data = stackalloc byte[10];
        Assert.False(Encoder.TryDecode("234", tag, data, out DecodeError? error, out string? msg, out (int Tag, int Data) length));
        Assert.Equal(DecodeError.NoSeparator, error);
        this.logger.WriteLine(msg);
    }

    [Fact]
    public void TryDecode_InvalidChecksum()
    {
        Span<char> tag = stackalloc char[10];
        Span<byte> data = stackalloc byte[10];
        Assert.False(Encoder.TryDecode("my14vf0uuur", tag, data, out DecodeError? error, out string? msg, out (int Tag, int Data) length));
        Assert.Equal(DecodeError.InvalidChecksum, error);
        this.logger.WriteLine(msg);
    }

    [Fact]
    public void TryDecode_ViolatesAlphabet()
    {
        Span<char> tag = stackalloc char[10];
        Span<byte> data = stackalloc byte[10];
        Assert.False(Encoder.TryDecode("my14vf0uuXr", tag, data, out DecodeError? error, out string? msg, out (int Tag, int Data) length));
        Assert.Equal(DecodeError.InvalidCharacter, error);
        this.logger.WriteLine(msg);
    }

    [Theory, MemberData(nameof(Pairings))]
    public void GetEncodedLength((string Tag, string HexData) human, string bech32Encoding)
    {
        int length = Encoder.GetEncodedLength(human.Tag.Length, human.HexData.Length / 2);
        Assert.Equal(bech32Encoding.Length, length);
    }

    [Fact]
    public void Decode_InputBufferTooSmall()
    {
        Assert.Throws<ArgumentException>(() =>
        {
            Span<char> tinyChars = stackalloc char[1];
            Span<byte> largeBytes = stackalloc byte[100];
            Encoder.Decode((string)Pairings[0][1]!, tinyChars, largeBytes);
        });
        Assert.Throws<ArgumentException>(() =>
        {
            Span<char> largeChars = stackalloc char[100];
            Span<byte> tinyBytes = stackalloc byte[1];
            Encoder.Decode((string)Pairings[0][1]!, largeChars, tinyBytes);
        });
    }
}
