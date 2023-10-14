// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Cryptocurrencies;

namespace Nerdbank.Zcash.App;

public class ZcashWallet
{
	public ZcashWallet()
	{
	}

	public Bip39Mnemonic Mnemonic { get; init; } = Bip39Mnemonic.Create(128);

	public SortedDictionary<uint, ZcashAccount> Accounts { get; } = new();
}
