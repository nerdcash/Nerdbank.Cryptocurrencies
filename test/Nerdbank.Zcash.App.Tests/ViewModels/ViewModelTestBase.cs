// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Linq;

public abstract class ViewModelTestBase
{
	protected MainViewModel MainViewModel { get; } = new();

	protected async Task InitializeWalletAsync()
	{
		FirstLaunchViewModel firstLaunch = new(this.MainViewModel);
		await firstLaunch.StartNewWalletCommand.Execute().FirstAsync();
	}
}
