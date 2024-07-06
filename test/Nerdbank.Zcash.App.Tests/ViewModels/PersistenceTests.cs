﻿// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Linq;

namespace ViewModels;

public class PersistenceTests : ViewModelTestBase
{
	public PersistenceTests()
		: base(persistStateAcrossReinitializations: true)
	{
	}

	[UIFact]
	public async Task CreateAndBackupAsync()
	{
		FirstLaunchViewModel firstLaunch = Assert.IsType<FirstLaunchViewModel>(this.MainViewModel.Content);
		await firstLaunch.StartNewWalletCommand.Execute().FirstAsync();

		HomeScreenViewModel homeScreen = Assert.IsType<HomeScreenViewModel>(this.MainViewModel.Content);
		Assert.True(homeScreen.IsBackupCommandPromoted);
		await homeScreen.BackupCommand.Execute().FirstAsync();

		BackupViewModel backup = Assert.IsType<BackupViewModel>(this.MainViewModel.Content);
		ExportSeedBasedAccountViewModel exportViewModel = Assert.IsType<ExportSeedBasedAccountViewModel>(backup.ExportAccountViewModel);
		exportViewModel.IsSeedPhraseBackedUp = true;
		await this.MainViewModel.HomeCommand.Execute().FirstAsync();

		homeScreen = Assert.IsType<HomeScreenViewModel>(this.MainViewModel.Content);
		Assert.False(homeScreen.IsBackupCommandPromoted);

		// Now 'restart' the program and onfirm that the account still claims to be backed up.
		await this.ReinitializeAppAsync();
		homeScreen = Assert.IsType<HomeScreenViewModel>(this.MainViewModel.Content);
		Assert.False(homeScreen.IsBackupCommandPromoted);
	}
}