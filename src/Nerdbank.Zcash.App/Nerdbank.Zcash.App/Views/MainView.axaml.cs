// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.ReactiveUI;
using Nerdbank.Zcash.App.ViewModels;

namespace Nerdbank.Zcash.App.Views;

public partial class MainView : ReactiveUserControl<MainViewModel>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="MainView"/> class.
	/// </summary>
	public MainView()
	{
		this.InitializeComponent();
	}

	protected override void OnLoaded(RoutedEventArgs e)
	{
		base.OnLoaded(e);

		if (TopLevel.GetTopLevel(this) is { } topLevel)
		{
			topLevel.BackRequested += this.MainView_BackRequested;
		}
	}

	protected override void OnUnloaded(RoutedEventArgs e)
	{
		if (TopLevel.GetTopLevel(this) is { } topLevel)
		{
			topLevel.BackRequested -= this.MainView_BackRequested;
		}

		base.OnUnloaded(e);
	}

	private void MainView_BackRequested(object? sender, RoutedEventArgs e)
	{
		if (this.ViewModel is { } mainViewModel && mainViewModel.NavigateBackCommand.CanExecute(null))
		{
			mainViewModel.NavigateBackCommand.Execute(null);
			e.Handled = true;
		}
	}
}
