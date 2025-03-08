// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public class InvalidAddressExceptionTests
{
	[Fact]
	public void Ctor_Default()
	{
		Assert.NotEqual(string.Empty, new InvalidAddressException().Message);
	}

	[Fact]
	public void Ctor_Message()
	{
		InvalidAddressException ex = new("test");
		Assert.Equal("test", ex.Message);
		Assert.Null(ex.InnerException);
	}

	[Fact]
	public void Ctor_InnerException()
	{
		Exception inner = new();
		InvalidAddressException ex = new("test", inner);
		Assert.Equal("test", ex.Message);
		Assert.Same(inner, ex.InnerException);
	}
}
