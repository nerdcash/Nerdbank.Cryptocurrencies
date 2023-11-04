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
		Account account1 = this.Model.AddAccount(1);
		account1.Name = "Checking";

		Account account3 = this.Model.AddAccount(3);
		account3.Name = "Savings";

		HDWallet deserialized = this.SerializeRoundtrip();
		Assert.Equal(this.Model.Accounts.Count, deserialized.Accounts.Count);
		Assert.Equal(this.Model.Accounts[1].Name, deserialized.Accounts[1].Name);
		Assert.Equal(this.Model.Accounts[3].Name, deserialized.Accounts[3].Name);
	}
}
