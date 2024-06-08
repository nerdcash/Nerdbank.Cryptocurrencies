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
		Assert.Equal(hash, receiver[..].ToArray());

		// Verify that a copy of the data has been made.
		hash[0] = 3;
		Assert.Equal(0, receiver[0]);
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

	[Fact]
	public void EqualityOfT()
	{
		byte[] hash = new byte[20];
		hash[1] = 2;
		TransparentP2SHReceiver receiver = new(hash.ToArray());
		TransparentP2SHReceiver receiver_copy = new(hash.ToArray());
		hash[3] = 3;
		TransparentP2SHReceiver receiver_unique = new(hash.ToArray());

		Assert.Equal(receiver, receiver_copy);
		Assert.NotEqual(receiver, receiver_unique);
	}

	[Fact]
	public void EqualsObjectOverride()
	{
		byte[] hash = new byte[20];
		hash[1] = 2;
		TransparentP2SHReceiver receiver = new(hash.ToArray());
		TransparentP2SHReceiver receiver_copy = new(hash.ToArray());
		hash[3] = 3;
		TransparentP2SHReceiver receiver_unique = new(hash.ToArray());

		Assert.True(receiver.Equals((object)receiver_copy));
		Assert.False(receiver.Equals((object)receiver_unique));
	}
}
