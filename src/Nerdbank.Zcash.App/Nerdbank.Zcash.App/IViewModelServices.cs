// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using Avalonia.Controls;

namespace Nerdbank.Zcash.App;

public interface IViewModelServices : IAppServices, INotifyPropertyChanged
{
	App App { get; }

	/// <summary>
	/// Gets or sets the most recently used account.
	/// </summary>
	Account? MostRecentlyUsedAccount { get; set; }

	TopLevel? TopLevel { get; }

	/// <summary>
	/// Pushes a view model onto the view stack.
	/// </summary>
	/// <typeparam name="T">The type of the view model.</typeparam>
	/// <param name="viewModel">The new view model.</param>
	/// <returns>The <paramref name="viewModel"/> value.</returns>
	/// <remarks>
	/// This will no-op if the given view model is already the current view model.
	/// </remarks>
	T NavigateTo<T>(T viewModel)
		where T : ViewModelBase;

	/// <summary>
	/// Pops the current view model off the view stack, effectively moving the view "back" one step.
	/// </summary>
	/// <param name="ifCurrentViewModel">The view model that is expected to be on top at the time of the call. If specified, the stack will only be popped if this is the top view model.</param>
	void NavigateBack(ViewModelBase? ifCurrentViewModel = null);

	/// <summary>
	/// Replaces the entire view stack with a new view model.
	/// </summary>
	/// <typeparam name="T">The type of the view model.</typeparam>
	/// <param name="viewModel">The new view model to select.</param>
	/// <returns>The <paramref name="viewModel"/> value.</returns>
	/// <remarks>
	/// This is useful primarily at the start of the app, when the user may not see the main home screen right away due to a first launch experience.
	/// </remarks>
	T ReplaceViewStack<T>(T viewModel)
		where T : ViewModelBase;
}
