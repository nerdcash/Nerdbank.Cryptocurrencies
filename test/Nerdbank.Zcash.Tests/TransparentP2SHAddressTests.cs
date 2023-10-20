// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public class TransparentP2SHAddressTests : TestBase
{
	[Fact]
	public void Ctor_Receiver()
	{
		byte[] hash = new byte[20];
		TransparentP2SHReceiver receiver = new(hash);
		TransparentP2SHAddress addr = new(receiver);
		Assert.Equal("t3JZcvsuaXE6ygokL4XUiZSTrQBUoPYFnXJ", addr.Address);
	}

	[Fact]
	public void Ctor_Receiver_TestNet()
	{
		byte[] hash = new byte[20];
		TransparentP2SHReceiver receiver = new(hash);
		TransparentP2SHAddress addr = new(receiver, ZcashNetwork.TestNet);
		Assert.Equal("t26YoyZ1iPgiMEWL4zGUm74eVWfhyDMXzY2", addr.Address);
		Assert.Equal(ZcashNetwork.TestNet, addr.Network);
	}

	[Fact]
	public void GetPoolReceiver()
	{
		Assert.NotNull(ZcashAddress.Decode(ValidTransparentP2SHAddress).GetPoolReceiver<TransparentP2SHReceiver>());
		Assert.Null(ZcashAddress.Decode(ValidTransparentP2SHAddress).GetPoolReceiver<TransparentP2PKHReceiver>());
		Assert.Null(ZcashAddress.Decode(ValidTransparentP2SHAddress).GetPoolReceiver<SaplingReceiver>());
	}
}
