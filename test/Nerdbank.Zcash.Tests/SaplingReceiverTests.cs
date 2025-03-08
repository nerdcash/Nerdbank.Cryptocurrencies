// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public class SaplingReceiverTests
{
	[Fact]
	public void Ctor()
	{
		byte[] d = new byte[88 / 8];
		byte[] pkd = new byte[256 / 8];
		d[1] = 1;
		pkd[1] = 2;
		SaplingReceiver receiver = new(d, pkd);
		Assert.Equal(d, receiver.D.ToArray());
		Assert.Equal(pkd, receiver.Pkd.ToArray());

		// Verify that a copy of the data has been made.
		d[0] = 3;
		Assert.Equal(0, receiver.D[0]);
	}

	[Fact]
	public void Ctor_ArgValidation()
	{
		Assert.Throws<ArgumentException>("d", () => new SaplingReceiver(new byte[1], new byte[256 / 8]));
		Assert.Throws<ArgumentException>("pkd", () => new SaplingReceiver(new byte[88 / 8], new byte[1]));
	}

	[Fact]
	public void Pool_Sapling() => Assert.Equal(Pool.Sapling, default(SaplingReceiver).Pool);

	[Fact]
	public void UnifiedReceiverTypeCode() => Assert.Equal(0x02, SaplingReceiver.UnifiedReceiverTypeCode);

	[Fact]
	public void EqualityOfT()
	{
		byte[] d = new byte[11];
		byte[] pkd = new byte[32];

		d[1] = 2;
		pkd[2] = 4;
		SaplingReceiver receiver = new(d.ToArray(), pkd.ToArray());
		SaplingReceiver receiver_copy = new(d.ToArray(), pkd.ToArray());
		d[3] = 3;
		SaplingReceiver receiver_unique = new(d.ToArray(), pkd.ToArray());
		pkd[5] = 8;
		SaplingReceiver receiver_unique2 = new(d.ToArray(), pkd.ToArray());

		Assert.Equal(receiver, receiver_copy);
		Assert.NotEqual(receiver, receiver_unique);
		Assert.NotEqual(receiver_unique2, receiver_unique);
	}

	[Fact]
	public void EqualsObjectOverride()
	{
		byte[] d = new byte[11];
		byte[] pkd = new byte[32];

		d[1] = 2;
		pkd[2] = 4;
		SaplingReceiver receiver = new(d.ToArray(), pkd.ToArray());
		SaplingReceiver receiver_copy = new(d.ToArray(), pkd.ToArray());
		d[3] = 3;
		SaplingReceiver receiver_unique = new(d.ToArray(), pkd.ToArray());
		pkd[5] = 8;
		SaplingReceiver receiver_unique2 = new(d.ToArray(), pkd.ToArray());

		Assert.True(receiver.Equals((object)receiver_copy));
		Assert.False(receiver.Equals((object)receiver_unique));
		Assert.False(receiver_unique2.Equals((object)receiver_unique));
	}
}
