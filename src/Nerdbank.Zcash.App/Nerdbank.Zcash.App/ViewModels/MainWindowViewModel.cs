// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using DynamicData.Binding;

namespace Nerdbank.Zcash.App.ViewModels;

public class MainWindowViewModel : MainViewModel
{
	private static readonly string AppTitle = Strings.AppTitle;
	private ObservableAsPropertyHelper<string> title;

	[Obsolete("Design-time only", error: true)]
	public MainWindowViewModel()
		: this(new App())
	{
	}

	public MainWindowViewModel(App app)
		: base(app)
	{
		this.title = this.WhenAnyValue<MainWindowViewModel, string, ViewModelBase?>(
			vm => vm.Content,
			content => content is IHasTitle titledViewModel ? $"{titledViewModel.Title} - {AppTitle}" : AppTitle)
			.ToProperty(this, nameof(this.Title));

		this.WatchSendProgressInAllAccounts();
	}

	public string Title => this.title.Value;

	public string BackCommandCaption => MainWindowStrings.BackCommandCaption;

	public string AppMenuCaption => MainWindowStrings.AppMenuCaption;

	public string HomeCommandCaption => MainWindowStrings.HomeCommandCaption;

	public string AddressBookCommandCaption => MainWindowStrings.AddressBookCommandCaption;

	public string AccountsListCommandCaption => MainWindowStrings.AccountsListCommandCaption;

	public string SettingsCommandCaption => MainWindowStrings.SettingsCommandCaption;

	public string BackupCommandCaption => MainWindowStrings.BackupCommandCaption;

	public string AboutCommandCaption => MainWindowStrings.AboutCommandCaption;

	public string TransactionsMenuCaption => MainWindowStrings.TransactionsMenuCaption;

	public string AccountBalanceCommandCaption => MainWindowStrings.AccountBalanceCommandCaption;

	public string TransactionHistoryCommandCaption => MainWindowStrings.TransactionHistoryCommandCaption;

	public string SendCommandCaption => MainWindowStrings.SendCommandCaption;

	public string ReceiveCommandCaption => MainWindowStrings.ReceiveCommandCaption;

	public string ToolsMenuCaption => MainWindowStrings.ToolsMenuCaption;

	public string AddressCheckCommandCaption => MainWindowStrings.AddressCheckCommandCaption;

	public SendProgressData SendProgress { get; } = new();

	private void WatchSendProgressInAllAccounts()
	{
		// Always present all the sends across all accounts in the app-wide progress indicator.
		this.SendProgress.SubscribeAndMerge(this.Wallet.Accounts.Select(a => a.SendProgress));
		this.Wallet.Accounts.ObserveCollectionChanges().Subscribe(
			_ => this.SendProgress.SubscribeAndMerge(this.Wallet.Accounts.Select(a => a.SendProgress)));
	}
}
