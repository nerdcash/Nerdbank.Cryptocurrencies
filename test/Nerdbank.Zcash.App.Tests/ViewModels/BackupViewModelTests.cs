// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Linq;

namespace ViewModels;

public class BackupViewModelTests : ViewModelTestBase
{
	private BackupViewModel viewModel;

	public BackupViewModelTests()
	{
		Account defaultAccount = this.MainViewModel.Wallet.Add(
			new ZcashAccount(new Zip32HDWallet(Bip39Mnemonic.Create(Zip32HDWallet.MinimumEntropyLengthInBits), ZcashNetwork.TestNet)));
		this.viewModel = new(this.MainViewModel);
	}

	[Fact]
	public async Task HiddenByDefaultAsync()
	{
		Assert.False(this.viewModel.IsRevealed);
		await this.viewModel.RevealCommand.Execute().FirstAsync();
		Assert.True(this.viewModel.IsRevealed);
	}
}
