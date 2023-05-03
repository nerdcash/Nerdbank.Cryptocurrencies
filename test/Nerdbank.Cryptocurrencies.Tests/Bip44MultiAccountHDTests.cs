// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public class Bip44MultiAccountHDTests
{
	[Fact]
	public void CreateKeyPath()
	{
		Assert.Equal("m/44'/133'/2'/3/4", Bip44MultiAccountHD.CreateKeyPath(0x80000085, 2, 3, 4).ToString());
		Assert.Equal("m/44'/133'/2'/3/4", Bip44MultiAccountHD.CreateKeyPath(133, 2 | Bip32HDWallet.KeyPath.HardenedBit, 3, 4).ToString());
	}

	/// <summary>
	/// Although it is not customary to harden the last two steps in the path, this test asserts that we allow it.
	/// </summary>
	[Fact]
	public void CreateKeyPath_HardenedLastParts()
	{
		Assert.Equal("m/44'/133'/2'/3'/4'", Bip44MultiAccountHD.CreateKeyPath(0x80000085, 2, 3 | Bip32HDWallet.KeyPath.HardenedBit, 4 | Bip32HDWallet.KeyPath.HardenedBit).ToString());
	}
}
