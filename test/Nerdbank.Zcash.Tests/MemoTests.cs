// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public class MemoTests
{
	private readonly ITestOutputHelper logger;

	public MemoTests(ITestOutputHelper logger)
	{
		this.logger = logger;
	}

	[Fact]
	public void DefaultMemo()
	{
		Assert.Equal(Zip302MemoFormat.MemoFormat.Message, default(Memo).MemoFormat);
		Assert.Equal(string.Empty, default(Memo).Message);
		Assert.True(default(Memo).ProprietaryData.IsEmpty);
		Assert.False(default(Memo).IsEmpty);
	}

	[Fact]
	public void DefaultMemo_Message()
	{
		Memo memo = default;
		memo.Message = "Hi there";
		Assert.Equal("Hi there", memo.Message);
		Assert.Equal(Zip302MemoFormat.MemoFormat.Message, memo.MemoFormat);
	}

	[Fact]
	public void DefaultMemo_Data()
	{
		Span<byte> data = stackalloc byte[511];
		data[0] = 13;

		Memo memo = default;
		memo.ProprietaryData = data;
		Assert.Equal(Zip302MemoFormat.MemoFormat.ProprietaryData, memo.MemoFormat);
		Assert.True(data.SequenceEqual(memo.ProprietaryData));
		Assert.Null(memo.Message);
	}

	[Fact]
	public void DefaultMemo_Clear()
	{
		Memo memo = default;
		memo.Clear();
		Assert.Equal(Zip302MemoFormat.MemoFormat.NoMemo, memo.MemoFormat);
	}

	[Fact]
	public void Memo_ToString()
	{
		Memo memo = default;
		this.logger.WriteLine(memo.ToString());
		Assert.Equal(string.Empty, memo.ToString());

		memo.Clear();
		this.logger.WriteLine(memo.ToString());

		memo.Message = "Hello world";
		this.logger.WriteLine(memo.ToString());
		Assert.Equal("Hello world", memo.ToString());

		Span<byte> data = stackalloc byte[511];
		data[0] = 13;
		memo.ProprietaryData = data;
		this.logger.WriteLine(memo.ToString());
	}
}
