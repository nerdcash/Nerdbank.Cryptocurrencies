// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash.App.ViewModels;

public class MainWindowViewModel : MainViewModel
{
	private static readonly string AppTitle = Strings.AppTitle;

	public MainWindowViewModel()
	{
		this.LinkProperty(nameof(this.Content), nameof(this.Title));
	}

	public string Title => this.Content is IHasTitle titledViewModel ? $"{titledViewModel.Title} - {AppTitle}" : AppTitle;
}
