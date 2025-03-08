// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public class TexReceiverTests
{
	[Fact]
	public void Ctor()
	{
		byte[] hash = new byte[20];
		hash[1] = 2;
		TexReceiver receiver = new(hash);
		Assert.Equal(hash, receiver[..]);

		// Verify that a copy of the data has been made.
		hash[0] = 3;
		Assert.Equal(0, receiver[0]);
	}

	[Fact]
	public void Ctor_ArgValidation()
	{
		Assert.Throws<ArgumentException>(() => new TexReceiver(new byte[1]));
	}

	[Fact]
	public void Pool_Transparent() => Assert.Equal(Pool.Transparent, default(TexReceiver).Pool);

	[Fact]
	public void TexToTransparentConversion()
	{
		Span<byte> p2pkh = stackalloc byte[20];
		Random.Shared.NextBytes(p2pkh);
		TexReceiver texReceiver = new(p2pkh);
		TransparentP2PKHReceiver transparentReceiver = (TransparentP2PKHReceiver)texReceiver;
		Assert.Equal(texReceiver[..], transparentReceiver[..]);
	}

	[Fact]
	public void TransparentToTexConversion()
	{
		Span<byte> p2pkh = stackalloc byte[20];
		Random.Shared.NextBytes(p2pkh);
		TransparentP2PKHReceiver transparentReceiver = new(p2pkh);
		TexReceiver texReceiver = transparentReceiver;
		Assert.Equal(transparentReceiver[..], texReceiver[..]);
	}

	[Fact]
	public void EqualityOfT()
	{
		byte[] hash = new byte[20];
		hash[1] = 2;
		TexReceiver receiver = new(hash.ToArray());
		TexReceiver receiver_copy = new(hash.ToArray());
		hash[3] = 3;
		TexReceiver receiver_unique = new(hash.ToArray());

		Assert.Equal(receiver, receiver_copy);
		Assert.NotEqual(receiver, receiver_unique);
	}

	[Fact]
	public void EqualsObjectOverride()
	{
		byte[] hash = new byte[20];
		hash[1] = 2;
		TexReceiver receiver = new(hash.ToArray());
		TexReceiver receiver_copy = new(hash.ToArray());
		hash[3] = 3;
		TexReceiver receiver_unique = new(hash.ToArray());

		Assert.True(receiver.Equals((object)receiver_copy));
		Assert.False(receiver.Equals((object)receiver_unique));
	}
}
