// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Windows.Input;
using Microsoft;

namespace Nerdbank.Zcash.App.ViewModels;

public class MainViewModel : ViewModelBase, IViewModelServicesWithWallet
{
	private readonly Stack<ViewModelBase> viewStack = new();
	private ZcashWallet? wallet;
	private ZcashAccount? selectedAccount;

	public MainViewModel()
	{
		this.NavigateBackCommand = ReactiveCommand.Create(
			() => this.NavigateBack(),
			this.WhenAnyValue(x => x.Content, new Func<ViewModelBase?, bool>(x => this.CanNavigateBack)));
		this.LinkProperty(nameof(this.Content), nameof(this.CanNavigateBack));

		this.NavigateTo(this.Wallet is null ? new FirstLaunchViewModel(this) : new HomeScreenViewModel(this));
	}

	public ZcashWallet? Wallet
	{
		get => this.wallet;
		set
		{
			if (this.wallet != value)
			{
				Verify.Operation(this.wallet is null, "Wallet can only be set once.");
				this.RaiseAndSetIfChanged(ref this.wallet, value);
			}
		}
	}

	ZcashWallet IViewModelServicesWithWallet.Wallet => this.Wallet ?? throw new InvalidOperationException();

	public ZcashAccount SelectedAccount
	{
		get => this.selectedAccount ?? this.Wallet?.Accounts.FirstOrDefault().Value ?? throw new InvalidOperationException();
		set => this.RaiseAndSetIfChanged(ref this.selectedAccount, value);
	}

	public ViewModelBase? Content
	{
		get => this.viewStack.TryPeek(out ViewModelBase? current) ? current : null;
	}

	/// <summary>
	/// Gets the command that navigates back one step in the view stack.
	/// </summary>
	public ICommand NavigateBackCommand { get; }

	public bool CanNavigateBack => this.viewStack.Count > 1;

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
