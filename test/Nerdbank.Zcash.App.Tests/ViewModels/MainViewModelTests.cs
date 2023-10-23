// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public class MainViewModelTests
{
	private MainViewModel viewModel = new();

	[Fact]
	public void FirstLaunch()
	{
		Assert.IsType<FirstLaunchViewModel>(this.viewModel.Content);
	}
}
