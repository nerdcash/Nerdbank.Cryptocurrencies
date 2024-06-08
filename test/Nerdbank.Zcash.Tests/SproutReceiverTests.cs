// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public class SproutReceiverTests
{
	[Fact]
	public void Ctor()
	{
		byte[] apk = new byte[256 / 8];
		byte[] pkenc = new byte[256 / 8];
		apk[1] = 1;
		pkenc[1] = 2;
		SproutReceiver receiver = new(apk, pkenc);
		Assert.Equal(apk, receiver.Apk.ToArray());
		Assert.Equal(pkenc, receiver.PkEnc.ToArray());

		// Verify that a copy of the data has been made.
		apk[0] = 3;
		Assert.Equal(0, receiver.Apk[0]);
	}

	[Fact]
	public void Ctor_ArgValidation()
	{
		Assert.Throws<ArgumentException>("apk", () => new SproutReceiver(new byte[1], new byte[256 / 8]));
		Assert.Throws<ArgumentException>("pkEnc", () => new SproutReceiver(new byte[256 / 8], new byte[1]));
	}

	[Fact]
	public void EqualityOfT()
	{
		byte[] apk = new byte[32];
		byte[] pkEnc = new byte[32];

		SproutReceiver receiver1a = new(apk.ToArray(), pkEnc.ToArray());
		SproutReceiver receiver1b = new(apk.ToArray(), pkEnc.ToArray());
		Assert.Equal(receiver1a, receiver1b);

		apk[0] = 1;
		SproutReceiver receiver2 = new(apk.ToArray(), pkEnc.ToArray());
		pkEnc[3] = 8;
		SproutReceiver receiver3 = new(apk.ToArray(), pkEnc.ToArray());
		Assert.NotEqual(receiver1a, receiver2);
		Assert.NotEqual(receiver3, receiver2);
	}

	[Fact]
	public void EqualsObjectOverride()
	{
		byte[] apk = new byte[32];
		byte[] pkEnc = new byte[32];

		SproutReceiver receiver1a = new(apk.ToArray(), pkEnc.ToArray());
		SproutReceiver receiver1b = new(apk.ToArray(), pkEnc.ToArray());
		Assert.True(receiver1a.Equals((object)receiver1b));

		apk[0] = 1;
		SproutReceiver receiver2 = new(apk.ToArray(), pkEnc.ToArray());
		pkEnc[3] = 8;
		SproutReceiver receiver3 = new(apk.ToArray(), pkEnc.ToArray());
		Assert.False(receiver1a.Equals((object)receiver2));
		Assert.False(receiver3.Equals((object)receiver2));
	}

	[Fact]
	public void Pool_Sprout() => Assert.Equal(Pool.Sprout, default(SproutReceiver).Pool);
}
