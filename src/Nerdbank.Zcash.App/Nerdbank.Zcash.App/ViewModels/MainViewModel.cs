// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Windows.Input;
using Avalonia.Controls;
using Microsoft;

namespace Nerdbank.Zcash.App.ViewModels;

public class MainViewModel : ViewModelBase, IViewModelServicesWithSelectedAccount
{
	private readonly Stack<ViewModelBase> viewStack = new();
	private ZcashAccount? selectedAccount;

	public MainViewModel()
	{
		this.NavigateBackCommand = ReactiveCommand.Create(
			() => this.NavigateBack(),
			this.WhenAnyValue(x => x.Content, new Func<ViewModelBase?, bool>(x => this.CanNavigateBack)));

		this.LinkProperty(nameof(this.Content), nameof(this.CanNavigateBack));
		this.LinkProperty(nameof(this.SelectedAccount), nameof(IViewModelServicesWithSelectedAccount.SelectedHDWallet));

		this.NavigateTo(this.Wallet.IsEmpty ? new FirstLaunchViewModel(this) : new HomeScreenViewModel(this));
	}

	public TopLevel? TopLevel { get; set; }

	public ZcashWallet Wallet { get; } = new();

	public ZcashAccount? SelectedAccount
	{
		get => this.selectedAccount ??= this.Wallet.AllAccounts.SelectMany(g => g).FirstOrDefault();
		set => this.RaiseAndSetIfChanged(ref this.selectedAccount, value);
	}

	public HDWallet? SelectedHDWallet => this.SelectedAccount is not null ? this.Wallet.GetHDWalletFor(this.SelectedAccount) : null;

	ZcashAccount IViewModelServicesWithSelectedAccount.SelectedAccount
	{
		get => this.SelectedAccount ?? throw new InvalidOperationException();
		set => this.SelectedAccount = value;
	}

	public IContactManager ContactManager { get; } = new ContactManager();

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
			if (viewModel.GetType() == this.Content?.GetType())
			{
				// Don't push the same view model type onto the stack twice.
				this.viewStack.Pop();
			}

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

	private ZcashAccount FindFirstAccount()
	{
		return this.Wallet.HDWallets.SelectMany(w => w.Accounts.Values).FirstOrDefault()
			?? this.Wallet.LoneAccounts.FirstOrDefault()
			?? throw new InvalidOperationException();
	}
}
