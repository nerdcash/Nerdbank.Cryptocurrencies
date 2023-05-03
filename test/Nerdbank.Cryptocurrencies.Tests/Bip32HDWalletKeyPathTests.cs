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
		Assert.Null(keyPath.Parent);

		keyPath = new KeyPath(4, keyPath);
		Assert.Equal(4u, keyPath.Index);
		Assert.Equal(0u, keyPath.Parent!.Index);
		Assert.Null(keyPath.Parent.Parent);

		keyPath = new KeyPath(2, keyPath);
		Assert.Equal(2u, keyPath.Index);
		Assert.Equal(4u, keyPath.Parent!.Index);
		Assert.Equal(0u, keyPath.Parent.Parent!.Index);
		Assert.Null(keyPath.Parent.Parent.Parent);
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
}
