// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Avalonia.Media.Imaging;

namespace Nerdbank.Zcash.App.ViewModels;

// Consider using +, - and = for receive, spend and balance buttons respectively.
public class HomeScreenViewModel : ViewModelBase
{
	private readonly IViewModelServices viewModelServices;
	private readonly ObservableAsPropertyHelper<bool> isBackupCommandPromoted;

	[Obsolete("Design-time only", error: true)]
	public HomeScreenViewModel()
		: this(new DesignTimeViewModelServices())
	{
	}

	public HomeScreenViewModel(IViewModelServices viewModelServices)
	{
		this.viewModelServices = viewModelServices;
		this.SelfUpdating = new UpdatingViewModel(this.viewModelServices.App.SelfUpdating);

		this.isBackupCommandPromoted = this.WhenAnyValue(x => x.viewModelServices.Wallet.HDWalletsRequireBackup).ToProperty(this, nameof(this.IsBackupCommandPromoted));

		IObservable<bool> canSpend = this.WhenAnyValue(x => x.viewModelServices.Wallet.AnyAccountCanSpend);

		this.ReceiveCommand = ReactiveCommand.Create(() => viewModelServices.NavigateTo(new ReceivingIntentSelectorViewModel(viewModelServices)));
		this.SendCommand = ReactiveCommand.Create(() => viewModelServices.NavigateTo(new SendingViewModel(viewModelServices)), canSpend);
		this.BalanceCommand = ReactiveCommand.Create(() => viewModelServices.NavigateTo(new BalanceViewModel(viewModelServices)));
		this.HistoryCommand = ReactiveCommand.Create(() => viewModelServices.NavigateTo(new HistoryViewModel(viewModelServices)));
		this.AddressBookCommand = ReactiveCommand.Create(() => viewModelServices.NavigateTo(new AddressBookViewModel(viewModelServices)));
		this.BackupCommand = ReactiveCommand.Create(() => viewModelServices.NavigateTo(new BackupViewModel(viewModelServices)));
	}

	public Bitmap Logo => Resources.AppLogo;

	public UpdatingViewModel SelfUpdating { get; }

	public string ReceiveCommandCaption => HomeScreenStrings.ReceiveCommandCaption;

	public ReactiveCommand<Unit, ReceivingIntentSelectorViewModel> ReceiveCommand { get; }

	public string SendCommandCaption => HomeScreenStrings.SendCommandCaption;

	public ReactiveCommand<Unit, SendingViewModel> SendCommand { get; }

	public string BalanceCommandCaption => HomeScreenStrings.BalanceCommandCaption;

	public ReactiveCommand<Unit, BalanceViewModel> BalanceCommand { get; }

	public string HistoryCommandCaption => HomeScreenStrings.HistoryCommandCaption;

	public ReactiveCommand<Unit, HistoryViewModel> HistoryCommand { get; }

	public string AddressBookCommandCaption => HomeScreenStrings.AddressBookCommandCaption;

	public ReactiveCommand<Unit, AddressBookViewModel> AddressBookCommand { get; }

	public bool IsBackupCommandPromoted => this.isBackupCommandPromoted.Value;

	public string BackupCommandCaption => HomeScreenStrings.BackupCommandCaption;

	public ReactiveCommand<Unit, BackupViewModel> BackupCommand { get; }

	public string BackupExplanation => HomeScreenStrings.BackupExplanation;
}
