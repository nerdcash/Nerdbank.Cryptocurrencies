// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash.App.ViewModels;

public class MainViewModel : ViewModelBase
{
	private ViewModelBase? content;

	public MainViewModel()
	{
		this.NavigateTo(new HomeScreenViewModel());
	}

	public ViewModelBase? Content
	{
		get => this.content;
		private set => this.RaiseAndSetIfChanged(ref this.content, value);
	}

	public void NavigateTo(ViewModelBase viewModel)
	{
		this.Content = viewModel;
	}
}
