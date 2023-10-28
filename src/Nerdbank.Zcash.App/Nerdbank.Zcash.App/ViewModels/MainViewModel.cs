// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Windows.Input;
using Avalonia.Controls;
using DynamicData.Binding;
using Microsoft;

namespace Nerdbank.Zcash.App.ViewModels;

public class MainViewModel : ViewModelBase, IViewModelServices
{
	private readonly Stack<ViewModelBase> viewStack = new();
	private Account? selectedAccount;
	private ObservableAsPropertyHelper<string?> contentTitle;

	public MainViewModel()
	{
		this.NavigateBackCommand = ReactiveCommand.Create(
			() => this.NavigateBack(),
			this.WhenAnyValue(x => x.Content, new Func<ViewModelBase?, bool>(x => this.CanNavigateBack)));

		this.contentTitle = this.WhenAnyValue<MainViewModel, string?, ViewModelBase?>(vm => vm.Content, content => (content as IHasTitle)?.Title).ToProperty(this, nameof(this.ContentTitle));

		IObservable<bool> accountSelected = this.WhenChanged(vm => vm.SelectedAccount, (vm, a) => a is not null);
		IObservable<bool> hdWalletSelected = this.WhenChanged(vm => vm.SelectedHDWallet, (vm, w) => w is not null);
		IObservable<bool> nonEmptyWallet = this.WhenAnyValue(vm => vm.Wallet.IsEmpty, empty => !empty);

		this.AboutCommand = ReactiveCommand.Create(() => this.NavigateTo(new AboutViewModel(this)));
		this.SettingsCommand = ReactiveCommand.Create(() => this.NavigateTo(new SettingsViewModel(this)));
		this.AddressBookCommand = ReactiveCommand.Create(() => this.NavigateTo(new AddressBookViewModel(this)), accountSelected);
		this.AddressCheckCommand = ReactiveCommand.Create(() => this.NavigateTo(new MatchAddressViewModel(this)));
		this.HomeCommand = ReactiveCommand.Create(() => this.ReplaceViewStack(this.GetHomeViewModel()));
		this.AccountsListCommand = ReactiveCommand.Create(() => this.NavigateTo(new AccountsViewModel(this)), nonEmptyWallet);
		this.AccountBalanceCommand = ReactiveCommand.Create(() => this.NavigateTo(new BalanceViewModel(this)), accountSelected);
		this.TransactionHistoryCommand = ReactiveCommand.Create(() => this.NavigateTo(new HistoryViewModel(this)), accountSelected);
		this.SendCommand = ReactiveCommand.Create(() => this.NavigateTo(new SendingViewModel(this)), accountSelected);
		this.ReceiveCommand = ReactiveCommand.Create(() => this.NavigateTo(new ReceivingIntentSelectorViewModel(this)), accountSelected);
		this.BackupCommand = ReactiveCommand.Create(() => this.NavigateTo(new BackupViewModel(this, this.SelectedHDWallet)), nonEmptyWallet);

		this.LinkProperty(nameof(this.Content), nameof(this.CanNavigateBack));
		this.LinkProperty(nameof(this.SelectedAccount), nameof(this.SelectedHDWallet));

		this.NavigateTo(this.GetHomeViewModel());
	}

	public TopLevel? TopLevel { get; set; }

	public AppSettings Settings => App.Instance.Settings;

	public ZcashWallet Wallet { get; } = new();

	public Account? SelectedAccount
	{
		get => this.selectedAccount ??= this.Wallet.AllAccounts.SelectMany(g => g).FirstOrDefault();
		set => this.RaiseAndSetIfChanged(ref this.selectedAccount, value);
	}

	public HDWallet? SelectedHDWallet => this.SelectedAccount?.MemberOf;

	public IContactManager ContactManager { get; } = new ContactManager();

	public ViewModelBase? Content
	{
		get => this.viewStack.TryPeek(out ViewModelBase? current) ? current : null;
	}

	public string? ContentTitle => this.contentTitle.Value;

	/// <summary>
	/// Gets the command that navigates back one step in the view stack.
	/// </summary>
	public ICommand NavigateBackCommand { get; }

	public bool CanNavigateBack => this.viewStack.Count > 1;

	public ReactiveCommand<Unit, Unit> AboutCommand { get; }

	public ReactiveCommand<Unit, Unit> HomeCommand { get; }

	public ReactiveCommand<Unit, Unit> AddressBookCommand { get; }

	public ReactiveCommand<Unit, Unit> SettingsCommand { get; }

	public ReactiveCommand<Unit, Unit> AddressCheckCommand { get; }

	public ReactiveCommand<Unit, Unit> AccountsListCommand { get; }

	public ReactiveCommand<Unit, Unit> AccountBalanceCommand { get; }

	public ReactiveCommand<Unit, Unit> TransactionHistoryCommand { get; }

	public ReactiveCommand<Unit, Unit> SendCommand { get; }

	public ReactiveCommand<Unit, Unit> ReceiveCommand { get; }

	public ReactiveCommand<Unit, Unit> BackupCommand { get; }

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

	public void NewAccount()
	{
		Verify.Operation(this.SelectedHDWallet is not null, "HD wallet must be selected first.");
		Account newAccount = this.SelectedHDWallet.AddAccount(this.SelectedHDWallet.MaxAccountIndex + 1);
		this.SelectedAccount = newAccount;
	}

	protected ViewModelBase GetHomeViewModel()
	{
		return this.Wallet.IsEmpty ? new FirstLaunchViewModel(this) : new HomeScreenViewModel(this);
	}

	private Account FindFirstAccount()
	{
		return this.Wallet.HDWallets.SelectMany(w => w.Accounts.Values).FirstOrDefault()
			?? this.Wallet.LoneAccounts.FirstOrDefault()
			?? throw new InvalidOperationException();
	}
}
