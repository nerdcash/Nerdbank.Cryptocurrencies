// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash.App.ViewModels;

public interface IViewModelServices
{
	/// <summary>
	/// Gets the wallet data model.
	/// </summary>
	ZcashWallet? Wallet { get; }

	/// <summary>
	/// Pushes a view model onto the view stack.
	/// </summary>
	/// <param name="viewModel">The new view model.</param>
	/// <remarks>
	/// This will no-op if the given view model is already the current view model.
	/// </remarks>
	void NavigateTo(ViewModelBase viewModel);

	/// <summary>
	/// Pops the current view model off the view stack, effectively moving the view "back" one step.
	/// </summary>
	/// <param name="ifCurrentViewModel">The view model that is expected to be on top at the time of the call. If specified, the stack will only be popped if this is the top view model.</param>
	void NavigateBack(ViewModelBase? ifCurrentViewModel = null);
}
