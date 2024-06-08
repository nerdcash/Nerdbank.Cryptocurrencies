// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public class TransparentP2PKHReceiverTests
{
	[Fact]
	public void Ctor()
	{
		byte[] hash = new byte[20];
		hash[1] = 2;
		TransparentP2PKHReceiver receiver = new(hash);
		Assert.Equal(hash, receiver[..]);

		// Verify that a copy of the data has been made.
		hash[0] = 3;
		Assert.Equal(0, receiver[0]);
	}

	[Fact]
	public void Ctor_ArgValidation()
	{
		Assert.Throws<ArgumentException>(() => new TransparentP2PKHReceiver(new byte[1]));
	}

	[Fact]
	public void Pool_Transparent() => Assert.Equal(Pool.Transparent, default(TransparentP2PKHReceiver).Pool);

	[Fact]
	public void UnifiedReceiverTypeCode() => Assert.Equal(0x00, TransparentP2PKHReceiver.UnifiedReceiverTypeCode);

	[Fact]
	public void EqualityOfT()
	{
		byte[] hash = new byte[20];
		hash[1] = 2;
		TransparentP2PKHReceiver receiver = new(hash.ToArray());
		TransparentP2PKHReceiver receiver_copy = new(hash.ToArray());
		hash[3] = 3;
		TransparentP2PKHReceiver receiver_unique = new(hash.ToArray());

		Assert.Equal(receiver, receiver_copy);
		Assert.NotEqual(receiver, receiver_unique);
	}

	[Fact]
	public void EqualsObjectOverride()
	{
		byte[] hash = new byte[20];
		hash[1] = 2;
		TransparentP2PKHReceiver receiver = new(hash.ToArray());
		TransparentP2PKHReceiver receiver_copy = new(hash.ToArray());
		hash[3] = 3;
		TransparentP2PKHReceiver receiver_unique = new(hash.ToArray());

		Assert.True(receiver.Equals((object)receiver_copy));
		Assert.False(receiver.Equals((object)receiver_unique));
	}
}
