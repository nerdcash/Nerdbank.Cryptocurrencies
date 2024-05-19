// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ViewModels;

public class ReceivingIntentSelectorViewModelTests : ViewModelTestBase
{
	private readonly ReceivingIntentSelectorViewModel viewModel;

	public ReceivingIntentSelectorViewModelTests()
	{
		this.viewModel = new(this.MainViewModel);
	}
}
