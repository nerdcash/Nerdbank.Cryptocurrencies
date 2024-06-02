// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public class TexReceiverTests
{
	[Fact]
	public void Ctor()
	{
		byte[] hash = new byte[20];
		hash[1] = 2;
		TexReceiver receiver = new(hash);
		Assert.Equal(hash, receiver.ValidatingKeyHash.ToArray());

		// Verify that a copy of the data has been made.
		hash[0] = 3;
		Assert.Equal(0, receiver.ValidatingKeyHash[0]);
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
		Assert.Equal(texReceiver.ValidatingKeyHash, transparentReceiver.ValidatingKeyHash);
	}

	[Fact]
	public void TransparentToTexConversion()
	{
		Span<byte> p2pkh = stackalloc byte[20];
		Random.Shared.NextBytes(p2pkh);
		TransparentP2PKHReceiver transparentReceiver = new(p2pkh);
		TexReceiver texReceiver = transparentReceiver;
		Assert.Equal(transparentReceiver.ValidatingKeyHash, texReceiver.ValidatingKeyHash);
	}
}
