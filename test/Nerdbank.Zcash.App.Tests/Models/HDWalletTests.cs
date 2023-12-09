// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Models;

public class HDWalletTests : ModelTestBase<HDWallet>
{
	public HDWalletTests(ITestOutputHelper logger)
		: base(logger)
	{
	}

	public HDWallet HDWallet { get; set; } = new(Mnemonic) { BirthdayHeight = 123456, Name = "Test HD" };

	public override HDWallet Model => this.HDWallet;

	[Fact]
	public void Serialize_Roundtrip()
	{
		this.Model.IsBackedUp = true;
		Assert.True(this.Model.IsDirty);

		HDWallet deserialized = this.SerializeRoundtrip();

		Assert.Equal(this.Model.Name, deserialized.Name);
		Assert.Equal(this.Model.BirthdayHeight, deserialized.BirthdayHeight);
		Assert.Equal(this.Model.MainNet, deserialized.MainNet);
		Assert.Equal(this.Model.TestNet, deserialized.TestNet);
		Assert.Equal(this.Model.IsBackedUp, deserialized.IsBackedUp);
		Assert.False(deserialized.IsDirty);
	}
}
