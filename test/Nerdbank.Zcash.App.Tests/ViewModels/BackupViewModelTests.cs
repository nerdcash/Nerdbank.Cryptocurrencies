// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Linq;
using Nerdbank.Zcash.App.Models;

public class BackupViewModelTests
{
	private MainViewModel mainViewModel = new();
	private BackupViewModel viewModel;

	public BackupViewModelTests()
	{
		Account defaultAccount = this.mainViewModel.Wallet.Add(
			new ZcashAccount(new Zip32HDWallet(Bip39Mnemonic.Create(Zip32HDWallet.MinimumEntropyLengthInBits), ZcashNetwork.TestNet)));
		this.mainViewModel.SelectedAccount = defaultAccount;
		this.viewModel = new(this.mainViewModel, null);
	}

	[Fact]
	public async Task HiddenByDefaultAsync()
	{
		Assert.False(this.viewModel.IsRevealed);
		await this.viewModel.RevealCommand.Execute().FirstAsync();
		Assert.True(this.viewModel.IsRevealed);
	}
}
