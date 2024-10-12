// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Linq;
using System.Windows.Input;
using Avalonia.Controls;
using DynamicData.Binding;
using Nerdbank.Cryptocurrencies.Exchanges;

namespace Nerdbank.Zcash.App.ViewModels;

public class MainViewModel : ViewModelBase, IViewModelServices
{
	private readonly Stack<ViewModelBase> viewStack = new();
	private readonly HttpClient httpClient = new() { DefaultRequestHeaders = { { "User-Agent", "Nerdbank.Zcash.App" } } };
	private readonly ObservableAsPropertyHelper<string?> contentTitle;
	private readonly ObservableAsPropertyHelper<SyncProgressData?> syncProgress;
	private readonly ObservableAsPropertyHelper<bool> isNonEmptyWallet;

	[Obsolete("Design-time only.", error: true)]
	public MainViewModel()
		: this(new App())
	{
	}

	public MainViewModel(App app)
	{
		this.App = app;

		this.ExchangeRateProvider = new LoggingExchangeRateProvider(new BinanceUSExchange(this.httpClient), app.PlatformServices.LoggerFactory.CreateLogger("Exchange rate"));
		this.HistoricalExchangeRateProvider = new Coinbase(this.httpClient);

		this.NavigateBackCommand = ReactiveCommand.Create(
			() => this.NavigateBack(),
			this.WhenAnyValue(x => x.Content, new Func<ViewModelBase?, bool>(x => this.CanNavigateBack)));

		this.contentTitle = this.WhenAnyValue<MainViewModel, string?, ViewModelBase?>(vm => vm.Content, content => (content as IHasTitle)?.Title).ToProperty(this, nameof(this.ContentTitle));

		this.isNonEmptyWallet = this.WhenAnyValue(vm => vm.Wallet.IsEmpty).Select(b => !b)
			.ToProperty(this, nameof(this.IsNonEmptyWallet));

		IObservable<bool> nonEmptyWallet = this.WhenAnyValue(vm => vm.Wallet.IsEmpty, empty => !empty);
		IObservable<bool> canSpend = this.WhenAnyValue(vm => vm.Wallet.AnyAccountCanSpend);

		this.AboutCommand = ReactiveCommand.Create(() => this.NavigateTo(new AboutViewModel(this)));
		this.SettingsCommand = ReactiveCommand.Create(() => this.NavigateTo(new SettingsViewModel(this)));
		this.AddressBookCommand = ReactiveCommand.Create(() => this.NavigateTo(new AddressBookViewModel(this)));
		this.AddressCheckCommand = ReactiveCommand.Create(() => this.NavigateTo(new MatchAddressViewModel(this)));
		this.LogsCommand = ReactiveCommand.Create(() => this.NavigateTo(new LogsViewModel(this)));
		this.HomeCommand = ReactiveCommand.Create(() => this.ReplaceViewStack(this.GetHomeViewModel()));
		this.AccountsListCommand = ReactiveCommand.Create(() => this.NavigateTo(new AccountsViewModel(this)), nonEmptyWallet);
		this.AccountBalanceCommand = ReactiveCommand.Create(() => this.NavigateTo(new BalanceViewModel(this)), nonEmptyWallet);
		this.TransactionHistoryCommand = ReactiveCommand.Create(() => this.NavigateTo(new HistoryViewModel(this)), nonEmptyWallet);
		this.SendCommand = ReactiveCommand.Create(() => this.NavigateTo(new SendingViewModel(this)), canSpend);
		this.ReceiveCommand = ReactiveCommand.Create(() => this.NavigateTo(new ReceivingIntentSelectorViewModel(this)), nonEmptyWallet);
		this.BackupCommand = ReactiveCommand.Create(() => this.NavigateTo(new BackupViewModel(this)), nonEmptyWallet);

		this.LinkProperty(nameof(this.Content), nameof(this.CanNavigateBack));
		this.LinkProperty(nameof(this.Content), nameof(this.IsNavigateBackVisible));

		this.WatchSendProgressInAllAccounts();
		this.syncProgress = this.WhenAnyValue<MainViewModel, SyncProgressData?, SyncProgressData?>(
			x => x.App.WalletSyncManager!.BlendedSyncProgress,
			p => p)
			.ToProperty(this, nameof(this.SyncProgress));

		this.NavigateTo(this.GetHomeViewModel());

		this.App.PlatformServices.ViewModelServices = this;
		this.App.WalletSyncManager?.StartSyncing(this, this.App.Data.Wallet);
	}

	public string BackCommandCaption => MainStrings.BackCommandCaption;

	public string AppMenuCaption => MainStrings.AppMenuCaption;

	public string HomeCommandCaption => MainStrings.HomeCommandCaption;

	public string AddressBookCommandCaption => MainStrings.AddressBookCommandCaption;

	public string AccountsListCommandCaption => MainStrings.AccountsListCommandCaption;

	public string SettingsCommandCaption => MainStrings.SettingsCommandCaption;

	public string BackupCommandCaption => MainStrings.BackupCommandCaption;

	public string AboutCommandCaption => MainStrings.AboutCommandCaption;

	public string TransactionsMenuCaption => MainStrings.TransactionsMenuCaption;

	public bool IsNonEmptyWallet => this.isNonEmptyWallet.Value;

	public string AccountBalanceCommandCaption => MainStrings.AccountBalanceCommandCaption;

	public string TransactionHistoryCommandCaption => MainStrings.TransactionHistoryCommandCaption;

	public string SendCommandCaption => MainStrings.SendCommandCaption;

	public string ReceiveCommandCaption => MainStrings.ReceiveCommandCaption;

	public string ToolsMenuCaption => MainStrings.ToolsMenuCaption;

	public string AddressCheckCommandCaption => MainStrings.AddressCheckCommandCaption;

	public string LogsCommandCaption => MainStrings.LogsCommandCaption;

	public App App { get; }

	public SyncProgressData? SyncProgress => this.syncProgress.Value;

	public virtual TopLevel? TopLevel { get; set; }

	public AppPlatformSettings AppPlatformSettings => this.App.AppPlatformSettings;

	public AppSettings Settings => this.App.Settings;

	public ZcashWallet Wallet => this.App.Data.Wallet;

	public Account? MostRecentlyUsedAccount { get; set; }

	public IContactManager ContactManager => this.App.Data.ContactManager;

	public ExchangeRateRecord ExchangeData => this.App.Data.ExchangeRates;

	public IExchangeRateProvider ExchangeRateProvider { get; set; }

	public IHistoricalExchangeRateProvider HistoricalExchangeRateProvider { get; set; }

	public SendProgressData SendProgress { get; } = new();

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

	public bool IsNavigateBackVisible => !this.App.PlatformServices.HasHardwareBackButton && this.CanNavigateBack;

	public ReactiveCommand<Unit, AboutViewModel> AboutCommand { get; }

	public bool IsAboutCommandVisible => !this.App.PlatformServices.HasAppLevelSystemMenu;

	public ReactiveCommand<Unit, ViewModelBase> HomeCommand { get; }

	public ReactiveCommand<Unit, AddressBookViewModel> AddressBookCommand { get; }

	public ReactiveCommand<Unit, SettingsViewModel> SettingsCommand { get; }

	public ReactiveCommand<Unit, MatchAddressViewModel> AddressCheckCommand { get; }

	public ReactiveCommand<Unit, LogsViewModel> LogsCommand { get; }

	public ReactiveCommand<Unit, AccountsViewModel> AccountsListCommand { get; }

	public ReactiveCommand<Unit, BalanceViewModel> AccountBalanceCommand { get; }

	public ReactiveCommand<Unit, HistoryViewModel> TransactionHistoryCommand { get; }

	public ReactiveCommand<Unit, SendingViewModel> SendCommand { get; }

	public ReactiveCommand<Unit, ReceivingIntentSelectorViewModel> ReceiveCommand { get; }

	public ReactiveCommand<Unit, BackupViewModel> BackupCommand { get; }

	public void RegisterSendTransactionTask(Task sendTask) => this.App.RegisterSendTransactionTask(sendTask);

	public T ReplaceViewStack<T>(T viewModel)
		where T : ViewModelBase
	{
		foreach (ViewModelBase vm in this.viewStack)
		{
			(vm as IDisposable)?.Dispose();
		}

		this.viewStack.Clear();
		this.NavigateTo(viewModel);
		return viewModel;
	}

	public T NavigateTo<T>(T viewModel)
		where T : ViewModelBase
	{
		if (this.Content != viewModel)
		{
			if (viewModel.GetType() == this.Content?.GetType())
			{
				// Don't push the same view model type onto the stack twice.
				(this.viewStack.Pop() as IDisposable)?.Dispose();
			}

			this.viewStack.Push(viewModel);
			this.RaisePropertyChanged(nameof(this.Content));
		}

		return viewModel;
	}

	public void NavigateBack(ViewModelBase? ifCurrentViewModel = null)
	{
		if (this.viewStack.Count > 1 && (ifCurrentViewModel is null || this.viewStack.Peek() == ifCurrentViewModel))
		{
			(this.viewStack.Pop() as IDisposable)?.Dispose();
			this.RaisePropertyChanged(nameof(this.Content));
		}
	}

	protected ViewModelBase GetHomeViewModel()
	{
		return this.Wallet.IsEmpty ? new FirstLaunchViewModel(this) : new HomeScreenViewModel(this);
	}

	private void WatchSendProgressInAllAccounts()
	{
		// Always present all the sends across all accounts in the app-wide progress indicator.
		this.SendProgress.SubscribeAndMerge(this.Wallet.Accounts.Select(a => a.SendProgress));
		this.Wallet.Accounts.ObserveCollectionChanges().Subscribe(
			_ => this.SendProgress.SubscribeAndMerge(this.Wallet.Accounts.Select(a => a.SendProgress)));
	}
}
