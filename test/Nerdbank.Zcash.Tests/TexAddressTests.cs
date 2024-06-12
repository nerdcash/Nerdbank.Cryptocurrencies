// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public class TexAddressTests
{
	[Fact]
	public void Ctor_FromTransparent()
	{
		TexAddress tex = new((TransparentP2PKHAddress)ZcashAddress.Decode("t1VmmGiyjVNeCjxDZzg7vZmd99WyzVby9yC"));
		Assert.Equal("tex1s2rt77ggv6q989lr49rkgzmh5slsksa9khdgte", tex.Address);
	}

	[Fact]
	public void SameReceiverAsTransparent()
	{
		var tex = (TexAddress)ZcashAddress.Decode("tex1s2rt77ggv6q989lr49rkgzmh5slsksa9khdgte");
		var tAddr = (TransparentP2PKHAddress)ZcashAddress.Decode("t1VmmGiyjVNeCjxDZzg7vZmd99WyzVby9yC");
		TransparentP2PKHReceiver tReceiver = tAddr.GetPoolReceiver<TransparentP2PKHReceiver>()!.Value;
		TexReceiver texReceiver = tex.GetPoolReceiver<TexReceiver>()!.Value;
		Assert.Equal(tReceiver[..], texReceiver[..]);
	}
}
