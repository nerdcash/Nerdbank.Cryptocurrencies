// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Models;

public class HDWalletTests : ModelTestBase<HDWallet>
{
	public HDWalletTests(ITestOutputHelper logger)
		: base(logger)
	{
	}

	public HDWallet HDWallet { get; set; } = new(new Zip32HDWallet(Mnemonic));

	public override HDWallet Model => this.HDWallet;

	[Fact]
	public void Serialize_Roundtrip()
	{
		this.Model.Name = "My Wallet";
		Assert.True(this.Model.IsDirty);

		HDWallet deserialized = this.SerializeRoundtrip();

		Assert.Equal(this.Model.Name, deserialized.Name);
		Assert.False(deserialized.IsDirty);
	}
}
