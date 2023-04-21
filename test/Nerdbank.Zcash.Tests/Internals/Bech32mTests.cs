// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Encoder = Nerdbank.Zcash.Utilities.Bech32m;

namespace Utilities;

public class Bech32mTests
{
    /// <summary>
    /// Gets the hex encoded and Bech32m encoded equivalents.
    /// </summary>
    public static object?[][] Pairings => new object?[][]
    {
        new[] { "c5f38b70305f519bf66d85fb6cf03058f3dde463ecd7918f2dc743918f2d", "split1checkupstagehandshakeupstreamerranterredcaperredlc445v" },
    };

    [Theory, MemberData(nameof(Pairings))]
    public static void Encode(string hexEncoding, string bech32mEncoding)
    {
        Span<byte> decodedBytes = Convert.FromHexString(hexEncoding);
        Span<char> encodedChars = stackalloc char[Encoder.GetMaximumEncodedLength(decodedBytes.Length)];
        int actualCount = Encoder.Encode(decodedBytes, encodedChars);
        Assert.Equal(bech32mEncoding, encodedChars.Slice(0, actualCount).ToString(), ignoreCase: false);
    }

    [Theory, MemberData(nameof(Pairings))]
    public static void Decode(string hexEncoding, string bech32mEncoding)
    {
        int expectedBytesWrittenCount = hexEncoding.Length / 2;

        Span<byte> actual = stackalloc byte[Encoder.GetMaximumDecodedLength(bech32mEncoding.Length)];
        int actualBytesWrittenCount = Encoder.Decode(bech32mEncoding, actual);

        Assert.Equal(expectedBytesWrittenCount, actualBytesWrittenCount);
        Assert.Equal(hexEncoding, Convert.ToHexString(actual.Slice(0, actualBytesWrittenCount)), ignoreCase: true);
    }

    [Fact]
    public void Decode_InputBufferTooSmall()
    {
        Assert.Throws<ArgumentException>(() =>
        {
            Span<byte> bytes = stackalloc byte[10];
            Encoder.Decode("split1checkupstagehandshakeupstreamerranterredcaperredlc445v", bytes);
        });
    }
}
