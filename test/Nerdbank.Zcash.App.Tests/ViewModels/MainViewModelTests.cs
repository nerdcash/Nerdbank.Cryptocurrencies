// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ViewModels;

public class MainViewModelTests : ViewModelTestBase
{
	[UIFact]
	public void FirstLaunch()
	{
		Assert.IsType<FirstLaunchViewModel>(this.MainViewModel.Content);
	}
}
