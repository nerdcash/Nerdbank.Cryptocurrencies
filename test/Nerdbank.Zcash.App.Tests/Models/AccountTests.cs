﻿// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Models;

public class AccountTests : ModelTestBase<Account>
{
	public AccountTests(ITestOutputHelper logger)
		: base(logger)
	{
	}

	public override Account Model => this.Account;

	public Account Account { get; set; } = new HDWallet(new Zip32HDWallet(Mnemonic, ZcashNetwork.TestNet)).AddAccount(1);

	[Fact]
	public void RoundTrip_HDAccount()
	{
		this.SerializeRoundtrip();
	}

	[Fact]
	public void RoundTrip_UFVKAccount()
	{
		this.Account = new(new ZcashAccount(this.Account.ZcashAccount.FullViewing!.UnifiedKey), null);
		this.SerializeRoundtrip();
	}

	[Fact]
	public void RoundTrip_UIVKAccount()
	{
		this.Account = new(new ZcashAccount(this.Account.ZcashAccount.IncomingViewing.UnifiedKey), null);
		this.SerializeRoundtrip();
	}
}
