// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Windows.Input;

namespace Nerdbank.Zcash.App.ViewModels;

public class MainViewModel : ViewModelBase, IViewModelServices
{
	private readonly Stack<ViewModelBase> viewStack = new();

	public MainViewModel()
	{
		this.NavigateBackCommand = ReactiveCommand.Create(
			() => this.NavigateBack(),
			this.WhenAnyValue(x => x.Content, new Func<ViewModelBase?, bool>(x => this.CanNavigateBack)));
		this.LinkProperty(nameof(this.Content), nameof(this.CanNavigateBack));

		this.NavigateTo(new HomeScreenViewModel(this));

		// TODO: Load this from a file when it exists.
		this.Wallet = new();
	}

	public ZcashWallet Wallet { get; }

	public ViewModelBase? Content
	{
		get => this.viewStack.TryPeek(out ViewModelBase? current) ? current : null;
	}

	/// <summary>
	/// Gets the command that navigates back one step in the view stack.
	/// </summary>
	public ICommand NavigateBackCommand { get; }

	public bool CanNavigateBack => this.viewStack.Count > 1;

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

	public void NavigateTo(ViewModelBase viewModel)
	{
		if (this.Content != viewModel)
		{
			this.viewStack.Push(viewModel);
			this.RaisePropertyChanged(nameof(this.Content));
		}
	}

	public void NavigateBack(ViewModelBase? ifCurrentViewModel = null)
	{
		if (this.viewStack.Count > 1 && (ifCurrentViewModel is null || this.viewStack.Peek() == ifCurrentViewModel))
		{
			this.viewStack.Pop();
			this.RaisePropertyChanged(nameof(this.Content));
		}
	}
}
