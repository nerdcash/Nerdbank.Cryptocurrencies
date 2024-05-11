// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ViewModels;

public class AccountsViewModelTests : ViewModelTestBase
{
	private AccountsViewModel viewModel = null!; // set in InitializeAsync

	public AccountsViewModelTests()
	{
	}

	public override async Task InitializeAsync()
	{
		await base.InitializeAsync();

		await this.InitializeWalletAsync();
		this.viewModel = new AccountsViewModel(this.MainViewModel);
	}
}
