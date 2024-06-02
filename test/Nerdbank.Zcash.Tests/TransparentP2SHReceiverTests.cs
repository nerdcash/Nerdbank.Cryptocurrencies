// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public class TransparentP2SHReceiverTests
{
	[Fact]
	public void Ctor()
	{
		byte[] hash = new byte[20];
		hash[1] = 2;
		TransparentP2SHReceiver receiver = new(hash);
		Assert.Equal(hash, receiver.ScriptHash.ToArray());

		// Verify that a copy of the data has been made.
		hash[0] = 3;
		Assert.Equal(0, receiver.ScriptHash[0]);
	}

	[Fact]
	public void Ctor_ArgValidation()
	{
		Assert.Throws<ArgumentException>(() => new TransparentP2SHReceiver(new byte[1]));
	}

	[Fact]
	public void Pool_Transparent() => Assert.Equal(Pool.Transparent, default(TransparentP2SHReceiver).Pool);

	[Fact]
	public void UnifiedReceiverTypeCode() => Assert.Equal(0x01, TransparentP2SHReceiver.UnifiedReceiverTypeCode);
}
