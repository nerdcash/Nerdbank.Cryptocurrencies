// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Linq;

namespace ViewModels;

public class PersistenceTests : ViewModelTestBase
{
	public PersistenceTests()
		: base(persistStateAcrossReinitializations: true)
	{
	}

	[Fact]
	public async Task CreateAndBackupAsync()
	{
		FirstLaunchViewModel firstLaunch = Assert.IsType<FirstLaunchViewModel>(this.MainViewModel.Content);
		await firstLaunch.StartNewWalletCommand.Execute().FirstAsync();

		HomeScreenViewModel homeScreen = Assert.IsType<HomeScreenViewModel>(this.MainViewModel.Content);
		await homeScreen.BackupCommand.Execute().FirstAsync();

		BackupViewModel backup = Assert.IsType<BackupViewModel>(this.MainViewModel.Content);
		backup.IsSeedPhraseBackedUp = true;
		await this.MainViewModel.HomeCommand.Execute().FirstAsync();

		homeScreen = Assert.IsType<HomeScreenViewModel>(this.MainViewModel.Content);
		Assert.True(homeScreen.IsSeedPhraseBackedUp);

		// Now 'restart' the program and onfirm that the account still claims to be backed up.
		await this.ReinitializeAppAsync();
		homeScreen = Assert.IsType<HomeScreenViewModel>(this.MainViewModel.Content);
		Assert.True(homeScreen.IsSeedPhraseBackedUp);
	}
}
