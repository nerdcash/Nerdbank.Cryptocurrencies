// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public class Bip32KeyPathTests
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

	public static object[][] ValidPathsRooted => new object[][]
	{
		new object[] { "m" },
		new object[] { "m/0" },
		new object[] { "m/0/3" },
		new object[] { "m/0/3'/4" },
		new object[] { "m/0/3'/4'" },
	};

	public static object[][] ValidPathsUnrooted => new object[][]
	{
		new object[] { "/0" },
		new object[] { "/0/3" },
		new object[] { "/0/3'/4" },
		new object[] { "/0/3'/4'" },
	};

	[Fact]
	public void KeyPath_Constructor_Unrooted()
	{
		Bip32KeyPath keyPath = new(0);
		Assert.Equal(0u, keyPath.Index);
		Assert.Null(keyPath.Parent);

		keyPath = new Bip32KeyPath(4, keyPath);
		Assert.Equal(4u, keyPath.Index);
		Assert.Equal(0u, keyPath.Parent!.Index);
		Assert.Null(keyPath.Parent.Parent);

		keyPath = new Bip32KeyPath(2, keyPath);
		Assert.Equal(2u, keyPath.Index);
		Assert.Equal(4u, keyPath.Parent!.Index);
		Assert.Equal(0u, keyPath.Parent.Parent!.Index);
		Assert.Null(keyPath.Parent.Parent.Parent);
	}

	[Fact]
	public void KeyPath_Constructor_Rooted()
	{
		Bip32KeyPath keyPath = new(0, Bip32KeyPath.Root);
		Assert.Equal(0u, keyPath.Index);
		Assert.Same(Bip32KeyPath.Root, keyPath.Parent);

		keyPath = new Bip32KeyPath(4, keyPath);
		Assert.Equal(4u, keyPath.Index);
		Assert.Equal(0u, keyPath.Parent!.Index);
		Assert.Same(Bip32KeyPath.Root, keyPath.Parent.Parent);

		keyPath = new Bip32KeyPath(2, keyPath);
		Assert.Equal(2u, keyPath.Index);
		Assert.Equal(4u, keyPath.Parent!.Index);
		Assert.Equal(0u, keyPath.Parent.Parent!.Index);
		Assert.Same(Bip32KeyPath.Root, keyPath.Parent.Parent.Parent);
	}

	[Fact]
	public void Index()
	{
		Assert.Equal(1u, new Bip32KeyPath(1).Index);
		Assert.Equal(1u, new Bip32KeyPath(1, Bip32KeyPath.Root).Index);

		Assert.Throws<InvalidOperationException>(() => Bip32KeyPath.Root.Index);
	}

	[Fact]
	public void Parent()
	{
		Assert.Null(new Bip32KeyPath(1).Parent);
		Assert.Same(Bip32KeyPath.Root, new Bip32KeyPath(1, Bip32KeyPath.Root).Parent);

		Assert.Null(Bip32KeyPath.Root.Parent);
	}

	[Fact]
	public void Equality()
	{
		Bip32KeyPath path1a = new(0, new(1));
		Bip32KeyPath path1b = new(0, new(1));
		Bip32KeyPath path2 = new(0, new(2));

		Assert.Equal(path1a, path1b);
		Assert.NotEqual(path1a, path2);

		Assert.NotEqual(new Bip32KeyPath(0), Bip32KeyPath.Root);
	}

	[Fact]
	public void IsHardened()
	{
		Assert.True(new Bip32KeyPath(0x80000000).IsHardened);
		Assert.True(new Bip32KeyPath(0x80000001).IsHardened);
		Assert.False(new Bip32KeyPath(0x7fffffff).IsHardened);
		Assert.False(new Bip32KeyPath(0).IsHardened);
		Assert.False(new Bip32KeyPath(1).IsHardened);
	}

	[Fact]
	public void Length()
	{
		Assert.Equal(0u, Bip32KeyPath.Root.Length);

		// Rooted
		Assert.Equal(1u, new Bip32KeyPath(0, Bip32KeyPath.Root).Length);
		Assert.Equal(1u, new Bip32KeyPath(1, Bip32KeyPath.Root).Length);
		Assert.Equal(2u, new Bip32KeyPath(2, new Bip32KeyPath(1, Bip32KeyPath.Root)).Length);

		// Unrooted
		Assert.Equal(1u, new Bip32KeyPath(0).Length);
		Assert.Equal(1u, new Bip32KeyPath(1).Length);
		Assert.Equal(2u, new Bip32KeyPath(2, new Bip32KeyPath(1)).Length);
	}

	[Fact]
	public void ToString_Path()
	{
		// Rooted
		Assert.Equal("m/0", new Bip32KeyPath(0, Bip32KeyPath.Root).ToString());
		Assert.Equal("m/0/4", new Bip32KeyPath(4, new(0, Bip32KeyPath.Root)).ToString());
		Assert.Equal("m/0/4'/6", new Bip32KeyPath(6, new(0x80000004, new(0, Bip32KeyPath.Root))).ToString());

		// Unrooted
		Assert.Equal("/0", new Bip32KeyPath(0).ToString());
		Assert.Equal("/0/4", new Bip32KeyPath(4, new(0)).ToString());
		Assert.Equal("/0/4'/6", new Bip32KeyPath(6, new(0x80000004, new(0))).ToString());
	}

	[Fact]
	public void IsRooted()
	{
		Assert.True(Bip32KeyPath.Root.IsRooted);
		Assert.True(new Bip32KeyPath(0, Bip32KeyPath.Root).IsRooted);
		Assert.True(new Bip32KeyPath(1, Bip32KeyPath.Root).IsRooted);
		Assert.True(Bip32KeyPath.Parse("m/1").IsRooted);

		Assert.False(new Bip32KeyPath(0).IsRooted);
		Assert.False(new Bip32KeyPath(1).IsRooted);
		Assert.False(Bip32KeyPath.Parse("/1").IsRooted);
	}

	[Theory, MemberData(nameof(ValidPathsRooted))]
	public void Parse(string path) => Assert.Equal(path, Bip32KeyPath.Parse(path).ToString());

	[Theory, MemberData(nameof(ValidPathsRooted))]
	public void TryParse_Rooted(string path)
	{
		Assert.True(Bip32KeyPath.TryParse(path, out Bip32KeyPath? result));
		Assert.Equal(path, result.ToString());
		Assert.True(result.IsRooted);
	}

	[Theory, MemberData(nameof(ValidPathsUnrooted))]
	public void TryParse_Unrooted(string path)
	{
		Assert.True(Bip32KeyPath.TryParse(path, out Bip32KeyPath? result));
		Assert.Equal(path, result.ToString());
		Assert.False(result.IsRooted);
	}

	[Fact]
	public void TryParse_RootReturnsSingleton()
	{
		Assert.Same(Bip32KeyPath.Root, Bip32KeyPath.Parse("m"));
	}

	[Fact]
	public void Parse_NullOrEmptyPath()
	{
		Assert.Throws<ArgumentException>(() => Bip32KeyPath.Parse(null!));
		Assert.Throws<ArgumentException>(() => Bip32KeyPath.Parse(string.Empty));
	}

	[Fact]
	public void TryParse_NullOrEmptyPath()
	{
		Assert.Throws<ArgumentException>(() => Bip32KeyPath.TryParse(null!, out _));
		Assert.Throws<ArgumentException>(() => Bip32KeyPath.TryParse(string.Empty, out _));
	}

	[Theory, MemberData(nameof(InvalidPaths))]
	public void Parse_InvalidPath(string path)
	{
		Assert.Throws<FormatException>(() => Bip32KeyPath.Parse(path));
	}

	[Theory, MemberData(nameof(InvalidPaths))]
	public void TryParse_InvalidPath(string path)
	{
		Assert.False(Bip32KeyPath.TryParse(path, out _));
	}

	[Fact]
	public void CompareTo_OddCases()
	{
		Assert.Equal(1, new Bip32KeyPath(0).CompareTo(null));
	}

	[Fact]
	public void CompareTo_SortingBehavior()
	{
		Bip32KeyPath[] unsorted = new[]
		{
			Bip32KeyPath.Parse("m/2/5"),
			Bip32KeyPath.Parse("m/2"),
			Bip32KeyPath.Parse("m/2/4"),
			Bip32KeyPath.Parse("m/2/4'"),
			Bip32KeyPath.Parse("m/3"),
		};

		Bip32KeyPath[] sorted = new[]
		{
			Bip32KeyPath.Parse("m/2"),
			Bip32KeyPath.Parse("m/2/4'"),
			Bip32KeyPath.Parse("m/2/4"),
			Bip32KeyPath.Parse("m/2/5"),
			Bip32KeyPath.Parse("m/3"),
		};

		Assert.Equal(sorted, unsorted.Order());
	}

	[Fact]
	public void Truncate_OutOfRange()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => Bip32KeyPath.Parse("m/0").Truncate(2));
	}

	[Fact]
	public void Truncate()
	{
		Assert.Same(Bip32KeyPath.Root, Bip32KeyPath.Parse("m/0").Truncate(0));

		Bip32KeyPath kp = Bip32KeyPath.Parse("m/0/1/2/3/4/5/6/7/8/9");
		Assert.Equal(2u, kp.Truncate(2).Length);
		Assert.Same(kp, kp.Truncate(10));
	}

	[Fact]
	public void Indexer_OutOfRange()
	{
		Assert.Throws<IndexOutOfRangeException>(() => Bip32KeyPath.Parse("m/0")[2]);
		Assert.Throws<IndexOutOfRangeException>(() => Bip32KeyPath.Parse("m/0")[0]);
	}

	[Fact]
	public void Indexer()
	{
		Bip32KeyPath kp = Bip32KeyPath.Parse("m/0/1/2/3/4/5/6/7/8/9");
		Assert.Equal(1u, kp[2]);
		Assert.Equal(9u, kp[10]);
	}

	[Fact]
	public void Root()
	{
		Assert.Equal("m", Bip32KeyPath.Root.ToString());
		Assert.Throws<IndexOutOfRangeException>(() => Bip32KeyPath.Root[0]);
		Assert.Throws<IndexOutOfRangeException>(() => Bip32KeyPath.Root[1]);
		Assert.Null(Bip32KeyPath.Root.Parent);
	}

	[Fact]
	public void Steps()
	{
		Assert.Empty(Bip32KeyPath.Root.Steps);

		// Rooted
		Assert.Equal(new uint[] { 1 }, Bip32KeyPath.Parse("m/1").Steps.Select(kp => kp.Index));
		Assert.Equal(new uint[] { 1, 3, 5 }, Bip32KeyPath.Parse("m/1/3/5").Steps.Select(kp => kp.Index));

		// Unrooted
		Assert.Equal(new uint[] { 1 }, Bip32KeyPath.Parse("/1").Steps.Select(kp => kp.Index));
		Assert.Equal(new uint[] { 1, 3, 5 }, Bip32KeyPath.Parse("/1/3/5").Steps.Select(kp => kp.Index));
	}

	[Fact]
	public void Append()
	{
		Bip32KeyPath original = Bip32KeyPath.Parse("m/1");
		Assert.Equal(Bip32KeyPath.Parse("m/1/3"), original.Append(3));
		Assert.Equal("m/1", original.ToString());
	}
}
