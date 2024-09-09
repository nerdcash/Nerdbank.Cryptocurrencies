// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.ReactiveUI;

namespace Nerdbank.Zcash.App.Views;

public partial class MainMobileView : ReactiveUserControl<MainMobileViewModel>
{
	public MainMobileView()
	{
		this.InitializeComponent();
	}

	protected override void OnLoaded(RoutedEventArgs e)
	{
		base.OnLoaded(e);

		if (TopLevel.GetTopLevel(this) is { } topLevel)
		{
			topLevel.BackRequested += this.MainMobileView_BackRequested;
		}
	}

	protected override void OnUnloaded(RoutedEventArgs e)
	{
		if (TopLevel.GetTopLevel(this) is { } topLevel)
		{
			topLevel.BackRequested -= this.MainMobileView_BackRequested;
		}

		base.OnUnloaded(e);
	}

	private void MainMobileView_BackRequested(object? sender, RoutedEventArgs e)
	{
		if (this.ViewModel is { } mainViewModel && mainViewModel.NavigateBackCommand.CanExecute(null))
		{
			mainViewModel.NavigateBackCommand.Execute(null);
			e.Handled = true;
		}
	}
}
