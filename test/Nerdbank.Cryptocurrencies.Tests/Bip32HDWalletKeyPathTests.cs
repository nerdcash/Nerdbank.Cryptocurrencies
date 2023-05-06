// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using KeyPath = Nerdbank.Cryptocurrencies.Bip32HDWallet.KeyPath;

public class Bip32HDWalletKeyPathTests
{
	public static object[][] InvalidPaths => new object[][]
	{
		new object[] { ";" },
		new object[] { "m0" },
		new object[] { "m/" },
		new object[] { "m/0/," },
		new object[] { "M/0" },
		new object[] { "m/0/q/3" },
		new object[] { "m/0/'/3" },
	};

	public static object[][] ValidPaths => new object[][]
	{
		new object[] { "m" },
		new object[] { "m/0" },
		new object[] { "m/0/3" },
		new object[] { "m/0/3'/4" },
		new object[] { "m/0/3'/4'" },
	};

	[Fact]
	public void KeyPath_Constructor()
	{
		KeyPath keyPath = new(0);
		Assert.Equal(0u, keyPath.Index);
		Assert.Same(KeyPath.Root, keyPath.Parent);

		keyPath = new KeyPath(4, keyPath);
		Assert.Equal(4u, keyPath.Index);
		Assert.Equal(0u, keyPath.Parent!.Index);
		Assert.Same(KeyPath.Root, keyPath.Parent.Parent);

		keyPath = new KeyPath(2, keyPath);
		Assert.Equal(2u, keyPath.Index);
		Assert.Equal(4u, keyPath.Parent!.Index);
		Assert.Equal(0u, keyPath.Parent.Parent!.Index);
		Assert.Same(KeyPath.Root, keyPath.Parent.Parent.Parent);
	}

	[Fact]
	public void Equality()
	{
		KeyPath path1a = new(0, new(1));
		KeyPath path1b = new(0, new(1));
		KeyPath path2 = new(0, new(2));

		Assert.Equal(path1a, path1b);
		Assert.NotEqual(path1a, path2);
	}

	[Fact]
	public void IsHardened()
	{
		Assert.True(new KeyPath(0x80000000).IsHardened);
		Assert.True(new KeyPath(0x80000001).IsHardened);
		Assert.False(new KeyPath(0x7fffffff).IsHardened);
		Assert.False(new KeyPath(0).IsHardened);
		Assert.False(new KeyPath(1).IsHardened);
	}

	[Fact]
	public void ToString_Path()
	{
		Assert.Equal("m/0", new KeyPath(0).ToString());
		Assert.Equal("m/0/4", new KeyPath(4, new(0)).ToString());
		Assert.Equal("m/0/4'/6", new KeyPath(6, new(0x80000004, new(0))).ToString());
	}

	[Theory, MemberData(nameof(ValidPaths))]
	public void Parse(string path) => Assert.Equal(path, KeyPath.Parse(path).ToString());

	[Theory, MemberData(nameof(ValidPaths))]
	public void TryParse(string path)
	{
		Assert.True(KeyPath.TryParse(path, out KeyPath? result));
		Assert.Equal(path, result.ToString());
	}

	[Fact]
	public void TryParse_RootReturnsSingleton()
	{
		Assert.Same(KeyPath.Root, KeyPath.Parse("m"));
	}

	[Fact]
	public void Parse_NullOrEmptyPath()
	{
		Assert.Throws<ArgumentException>(() => KeyPath.Parse(null!));
		Assert.Throws<ArgumentException>(() => KeyPath.Parse(string.Empty));
	}

	[Fact]
	public void TryParse_NullOrEmptyPath()
	{
		Assert.Throws<ArgumentException>(() => KeyPath.TryParse(null!, out _));
		Assert.Throws<ArgumentException>(() => KeyPath.TryParse(string.Empty, out _));
	}

	[Theory, MemberData(nameof(InvalidPaths))]
	public void Parse_InvalidPath(string path)
	{
		Assert.Throws<FormatException>(() => KeyPath.Parse(path));
	}

	[Theory, MemberData(nameof(InvalidPaths))]
	public void TryParse_InvalidPath(string path)
	{
		Assert.False(KeyPath.TryParse(path, out _));
	}

	[Fact]
	public void CompareTo_OddCases()
	{
		Assert.Equal(1, new KeyPath(0).CompareTo(null));
	}

	[Fact]
	public void CompareTo_SortingBehavior()
	{
		KeyPath[] unsorted = new[]
		{
			KeyPath.Parse("m/2/5"),
			KeyPath.Parse("m/2"),
			KeyPath.Parse("m/2/4"),
			KeyPath.Parse("m/2/4'"),
			KeyPath.Parse("m/3"),
		};

		KeyPath[] sorted = new[]
		{
			KeyPath.Parse("m/2"),
			KeyPath.Parse("m/2/4'"),
			KeyPath.Parse("m/2/4"),
			KeyPath.Parse("m/2/5"),
			KeyPath.Parse("m/3"),
		};

		Assert.Equal(sorted, unsorted.Order());
	}

	[Fact]
	public void Truncate_OutOfRange()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => KeyPath.Parse("m/0").Truncate(2));
	}

	[Fact]
	public void Truncate()
	{
		Assert.Same(KeyPath.Root, KeyPath.Parse("m/0").Truncate(0));

		KeyPath kp = KeyPath.Parse("m/0/1/2/3/4/5/6/7/8/9");
		Assert.Equal(2u, kp.Truncate(2).Length);
		Assert.Same(kp, kp.Truncate(10));
	}

	[Fact]
	public void Indexer_OutOfRange()
	{
		Assert.Throws<IndexOutOfRangeException>(() => KeyPath.Parse("m/0")[2]);
		Assert.Throws<IndexOutOfRangeException>(() => KeyPath.Parse("m/0")[0]);
	}

	[Fact]
	public void Indexer()
	{
		KeyPath kp = KeyPath.Parse("m/0/1/2/3/4/5/6/7/8/9");
		Assert.Equal(1u, kp[2]);
		Assert.Equal(9u, kp[10]);
	}

	[Fact]
	public void Root()
	{
		Assert.Equal("m", KeyPath.Root.ToString());
		Assert.Throws<IndexOutOfRangeException>(() => KeyPath.Root[0]);
		Assert.Throws<IndexOutOfRangeException>(() => KeyPath.Root[1]);
		Assert.Null(KeyPath.Root.Parent);
	}

	[Fact]
	public void Enumerator()
	{
		Assert.Empty(KeyPath.Root);
		Assert.Equal(new uint[] { 1 }, KeyPath.Parse("m/1"	));
		Assert.Equal(new uint[] { 1, 3, 5 }, KeyPath.Parse("m/1/3/5"));
	}
}
