// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public class TransparentP2PKHAddressTests : TestBase
{
	[Fact]
	public void Ctor_Receiver()
	{
		byte[] hash = new byte[20];
		hash[1] = 2;
		TransparentP2PKHReceiver receiver = new(hash);
		TransparentP2PKHAddress addr = new(receiver);
		Assert.Equal("t1HseQJEmpT7jcnTGoJVsKg5fuTzhfNXu9v", addr.Address);
	}

	[Fact]
	public void GetPoolReceiver()
	{
		Assert.NotNull(ZcashAddress.Parse(ValidTransparentP2PKHAddress).GetPoolReceiver<TransparentP2PKHReceiver>());
		Assert.Null(ZcashAddress.Parse(ValidTransparentP2PKHAddress).GetPoolReceiver<TransparentP2SHReceiver>());
		Assert.Null(ZcashAddress.Parse(ValidTransparentP2PKHAddress).GetPoolReceiver<SaplingReceiver>());
	}
}
