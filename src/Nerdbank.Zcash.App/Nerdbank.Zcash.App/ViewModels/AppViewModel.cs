// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash.App.ViewModels;

/// <summary>
/// The view model behind the <see cref="App" /> class.
/// </summary>
public class AppViewModel
{
	public AppViewModel(IViewModelServices viewModelServices)
	{
		this.AboutCommand = ReactiveCommand.Create(() => viewModelServices.NavigateTo(new AboutViewModel(viewModelServices)));
	}

	public string AboutCommandCaption => MainStrings.AboutCommandCaption;

	public ReactiveCommand<Unit, AboutViewModel> AboutCommand { get; }
}
