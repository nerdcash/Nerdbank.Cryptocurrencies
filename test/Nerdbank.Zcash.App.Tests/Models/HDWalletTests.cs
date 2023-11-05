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
	public void Serialize_WithAccounts()
	{
		this.Model.Name = "My Wallet";
		this.Model.BirthdayHeight = 123456;
		this.Model.IsSeedPhraseBackedUp = true;
		Assert.True(this.Model.IsDirty);

		HDWallet deserialized = this.SerializeRoundtrip();

		Assert.Equal(this.Model.Name, deserialized.Name);
		Assert.Equal(this.Model.BirthdayHeight, deserialized.BirthdayHeight);
		Assert.Equal(this.Model.IsSeedPhraseBackedUp, deserialized.IsSeedPhraseBackedUp);
		Assert.False(deserialized.IsDirty);
	}
}
