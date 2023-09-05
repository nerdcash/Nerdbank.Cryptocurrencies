// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public class Zip302MemoFormatTests : TestBase
{
	private readonly ITestOutputHelper logger;

	public Zip302MemoFormatTests(ITestOutputHelper logger)
	{
		this.logger = logger;
	}

	[Fact]
	public void EncodeMessage()
	{
		Span<byte> actualMemo = stackalloc byte[512];
		Zip302MemoFormat.EncodeMessage("abc", actualMemo);

		Span<byte> expectedMemo = stackalloc byte[512];
		"abc"u8.CopyTo(expectedMemo);

		Assert.True(actualMemo.SequenceEqual(expectedMemo));
	}

	[Fact]
	public void EncodeMessage_EmptyMessage()
	{
		Span<byte> actualMemo = stackalloc byte[512];
		Zip302MemoFormat.EncodeMessage(default, actualMemo);

		Span<byte> expectedMemo = stackalloc byte[512];
		Assert.True(actualMemo.SequenceEqual(expectedMemo));
	}

	[Fact]
	public void EncodeMessage_BufferNotExactSize()
	{
		ArgumentException ex = Assert.Throws<ArgumentException>("memo", () => Zip302MemoFormat.EncodeMessage(default, stackalloc byte[511]));
		this.logger.WriteLine(ex.Message);

		ex = Assert.Throws<ArgumentException>("memo", () => Zip302MemoFormat.EncodeMessage(default, stackalloc byte[513]));
		this.logger.WriteLine(ex.Message);
	}

	[Fact]
	public void EncodeMessage_TooMuchText()
	{
		ArgumentException ex = Assert.Throws<ArgumentException>("text", () => Zip302MemoFormat.EncodeMessage(new string('a', 513), stackalloc byte[512]));
		this.logger.WriteLine(ex.Message);
	}

	[Fact]
	public void TryDecodeMessage_EmptyString()
	{
		Assert.True(Zip302MemoFormat.TryDecodeMessage(stackalloc byte[512], out string? text));
		Assert.Equal(string.Empty, text);
	}

	[Fact]
	public void TryDecodeMessage_SomeText()
	{
		Span<byte> memo = stackalloc byte[512];
		"abc"u8.CopyTo(memo);
		Assert.True(Zip302MemoFormat.TryDecodeMessage(memo, out string? text));
		Assert.Equal("abc", text);
	}

	[Fact]
	public void TryDecodeMessage_MemoWrongSize()
	{
		ArgumentException ex = Assert.Throws<ArgumentException>("memo", () => Zip302MemoFormat.TryDecodeMessage(stackalloc byte[511], out _));
		this.logger.WriteLine(ex.Message);

		ex = Assert.Throws<ArgumentException>("memo", () => Zip302MemoFormat.TryDecodeMessage(stackalloc byte[513], out _));
		this.logger.WriteLine(ex.Message);
	}

	[Fact]
	public void TryDecodeMessage_NotAMessage()
	{
		Span<byte> memo = stackalloc byte[512];

		memo[0] = 0xF6;
		Assert.False(Zip302MemoFormat.TryDecodeMessage(memo, out string? text));
		Assert.Null(text);

		memo[0] = 0xF5;
		Assert.False(Zip302MemoFormat.TryDecodeMessage(memo, out text));
		Assert.Null(text);

		memo[0] = 0xF7;
		Assert.False(Zip302MemoFormat.TryDecodeMessage(memo, out text));
		Assert.Null(text);
	}

	[Fact]
	public void EncodeNoMemo()
	{
		Span<byte> actualMemo = stackalloc byte[512];
		Zip302MemoFormat.EncodeNoMemo(actualMemo);

		Span<byte> expectedMemo = stackalloc byte[512];
		expectedMemo[0] = 0xF6;
		Assert.True(expectedMemo.SequenceEqual(actualMemo));
	}

	[Fact]
	public void EncodeNoMemo_BufferNotExactSize()
	{
		ArgumentException ex = Assert.Throws<ArgumentException>("memo", () => Zip302MemoFormat.EncodeNoMemo(stackalloc byte[511]));
		this.logger.WriteLine(ex.Message);

		ex = Assert.Throws<ArgumentException>("memo", () => Zip302MemoFormat.EncodeNoMemo(stackalloc byte[513]));
		this.logger.WriteLine(ex.Message);
	}

	[Fact]
	public void EncodeProprietaryData_MaxSize()
	{
		Span<byte> privateData = stackalloc byte[511];
		privateData.Fill(13);

		Span<byte> actualMemo = stackalloc byte[512];
		Zip302MemoFormat.EncodeProprietaryData(privateData, actualMemo);

		Span<byte> expectedMemo = stackalloc byte[512];
		expectedMemo[0] = 0xFF;
		privateData.CopyTo(expectedMemo[1..]);
		Assert.True(expectedMemo.SequenceEqual(actualMemo));
	}

	[Fact]
	public void EncodeProprietaryData_LesserSize()
	{
		Span<byte> privateData = stackalloc byte[11];
		privateData.Fill(13);

		Span<byte> actualMemo = stackalloc byte[512];
		Zip302MemoFormat.EncodeProprietaryData(privateData, actualMemo);

		Span<byte> expectedMemo = stackalloc byte[512];
		expectedMemo[0] = 0xFF;
		privateData.CopyTo(expectedMemo[1..]);
		Assert.True(expectedMemo.SequenceEqual(actualMemo));
	}

	[Fact]
	public void EncodeProprietaryData_PrivateDataTooLarge()
	{
		ArgumentException ex = Assert.Throws<ArgumentException>("data", () => Zip302MemoFormat.EncodeProprietaryData(stackalloc byte[512], stackalloc byte[512]));
		this.logger.WriteLine(ex.Message);
	}

	[Fact]
	public void EncodeProprietaryData_BufferNotExactSize()
	{
		ArgumentException ex = Assert.Throws<ArgumentException>("memo", () => Zip302MemoFormat.EncodeProprietaryData(stackalloc byte[5], stackalloc byte[511]));
		this.logger.WriteLine(ex.Message);

		ex = Assert.Throws<ArgumentException>("memo", () => Zip302MemoFormat.EncodeProprietaryData(stackalloc byte[5], stackalloc byte[513]));
		this.logger.WriteLine(ex.Message);
	}

	[Fact]
	public void TryDecodeProprietaryData_MemoWrongSize()
	{
		ArgumentException ex = Assert.Throws<ArgumentException>("memo", () => Zip302MemoFormat.TryDecodeProprietaryData(stackalloc byte[511], stackalloc byte[511]));
		this.logger.WriteLine(ex.Message);

		ex = Assert.Throws<ArgumentException>("memo", () => Zip302MemoFormat.TryDecodeProprietaryData(stackalloc byte[513], stackalloc byte[511]));
		this.logger.WriteLine(ex.Message);
	}

	[Fact]
	public void TryDecodeProprietaryData_DataWrongSize()
	{
		byte[] memo = new byte[512];
		memo[0] = 0xff;

		ArgumentException ex = Assert.Throws<ArgumentException>("data", () => Zip302MemoFormat.TryDecodeProprietaryData(memo, stackalloc byte[510]));
		this.logger.WriteLine(ex.Message);

		ex = Assert.Throws<ArgumentException>("data", () => Zip302MemoFormat.TryDecodeProprietaryData(memo, stackalloc byte[512]));
		this.logger.WriteLine(ex.Message);
	}

	[Fact]
	public void TryDecodeProprietaryData_NotData()
	{
		Assert.False(Zip302MemoFormat.TryDecodeProprietaryData(stackalloc byte[512], stackalloc byte[511]));
	}

	[Fact]
	public void TryDecodeProprietaryData()
	{
		Span<byte> memo = stackalloc byte[512];
		memo[0] = 0xff;
		memo[1..].Fill(13);

		Span<byte> data = stackalloc byte[511];
		Assert.True(Zip302MemoFormat.TryDecodeProprietaryData(memo, data));
		Assert.True(memo[1..].SequenceEqual(data));
	}

	[Fact]
	public void DetectMemoFormat_MemoWrongSize()
	{
		ArgumentException ex = Assert.Throws<ArgumentException>("memo", () => Zip302MemoFormat.TryDecodeMessage(stackalloc byte[511], out _));
		this.logger.WriteLine(ex.Message);

		ex = Assert.Throws<ArgumentException>("memo", () => Zip302MemoFormat.TryDecodeMessage(stackalloc byte[513], out _));
		this.logger.WriteLine(ex.Message);
	}

	[Fact]
	public void DetectMemoFormat()
	{
		Span<byte> memo = stackalloc byte[512];
		Assert.Equal(Zip302MemoFormat.MemoFormat.Message, Zip302MemoFormat.DetectMemoFormat(memo));

		memo[0] = 0xFF;
		Assert.Equal(Zip302MemoFormat.MemoFormat.ProprietaryData, Zip302MemoFormat.DetectMemoFormat(memo));

		memo[0] = 0xF6;
		Assert.Equal(Zip302MemoFormat.MemoFormat.NoMemo, Zip302MemoFormat.DetectMemoFormat(memo));

		memo[0] = 0xF5;
		Assert.Equal(Zip302MemoFormat.MemoFormat.Reserved, Zip302MemoFormat.DetectMemoFormat(memo));

		memo[0] = 0xF6;
		memo[511] = 0x13;
		Assert.Equal(Zip302MemoFormat.MemoFormat.Reserved, Zip302MemoFormat.DetectMemoFormat(memo));
	}
}
