// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Windows.Input;

namespace Nerdbank.Zcash.App.ViewModels;

public class MainViewModel : ViewModelBase
{
	private readonly Stack<ViewModelBase> viewStack = new();

	public MainViewModel()
	{
		this.NavigateBackCommand = ReactiveCommand.Create(
			() => this.NavigateBack(),
			this.WhenAnyValue(x => x.Content, new Func<ViewModelBase?, bool>(x => this.viewStack.Count > 1)));

		this.NavigateTo(new HomeScreenViewModel());
	}

	public ViewModelBase? Content
	{
		get => this.viewStack.TryPeek(out ViewModelBase? current) ? current : null;
	}

	/// <summary>
	/// Gets the command that navigates back one step in the view stack.
	/// </summary>
	public ICommand NavigateBackCommand { get; }

	/// <summary>
	/// Replaces the entire view stack with a new view model.
	/// </summary>
	/// <param name="viewModel">The new view model to select.</param>
	/// <remarks>
	/// This is useful primarily at the start of the app, when the user may not see the main home screen right away due to a first launch experience.
	/// </remarks>
	public void ReplaceViewStack(ViewModelBase viewModel)
	{
		this.viewStack.Clear();
		this.NavigateTo(viewModel);
	}

	/// <summary>
	/// Pushes a view model onto the view stack.
	/// </summary>
	/// <param name="viewModel">The new view model.</param>
	/// <remarks>
	/// This will no-op if the given view model is already the current view model.
	/// </remarks>
	public void NavigateTo(ViewModelBase viewModel)
	{
		if (this.Content != viewModel)
		{
			this.viewStack.Push(viewModel);
			this.RaisePropertyChanged(nameof(this.Content));
		}
	}

	/// <summary>
	/// Pops the current view model off the view stack, effectively moving the view "back" one step.
	/// </summary>
	/// <param name="ifCurrentViewModel">The view model that is expected to be on top at the time of the call. If specified, the stack will only be popped if this is the top view model.</param>
	public void NavigateBack(ViewModelBase? ifCurrentViewModel = null)
	{
		if (this.viewStack.Count > 1 && (ifCurrentViewModel is null || this.viewStack.Peek() == ifCurrentViewModel))
		{
			this.viewStack.Pop();
			this.RaisePropertyChanged(nameof(this.Content));
		}
	}
}
