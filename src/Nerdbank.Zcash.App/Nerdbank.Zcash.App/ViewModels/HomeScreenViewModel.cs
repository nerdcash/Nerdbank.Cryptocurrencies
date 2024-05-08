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

		this.isBackupCommandPromoted = this.WhenAnyValue(x => x.viewModelServices.Wallet.HDWalletsRequireBackup).ToProperty(this, nameof(this.IsBackupCommandPromoted));

		this.ReceiveCommand = ReactiveCommand.Create(() => viewModelServices.NavigateTo(new ReceivingIntentSelectorViewModel(viewModelServices)));
		this.SendCommand = ReactiveCommand.Create(() => viewModelServices.NavigateTo(new SendingViewModel(viewModelServices)));
		this.BalanceCommand = ReactiveCommand.Create(() => viewModelServices.NavigateTo(new BalanceViewModel(viewModelServices)));
		this.HistoryCommand = ReactiveCommand.Create(() => viewModelServices.NavigateTo(new HistoryViewModel(viewModelServices)));
		this.BackupCommand = ReactiveCommand.Create(() => viewModelServices.NavigateTo(new BackupViewModel(viewModelServices)));
	}

	public Bitmap Logo => Resources.AppLogo;

	public UpdatingViewModel SelfUpdating => this.viewModelServices.App.SelfUpdating;

	public string ReceiveCommandCaption => "📥 Receive";

	public ReactiveCommand<Unit, ReceivingIntentSelectorViewModel> ReceiveCommand { get; }

	public string ReceiveExplanation => "Share your Zcash address or a payment request with others so they can send you Zcash.";

	public string SendCommandCaption => "📤 Send";

	public ReactiveCommand<Unit, SendingViewModel> SendCommand { get; }

	public string SendExplanation => "Send someone Zcash.";

	public string BalanceCommandCaption => "⚖️ Balance";

	public ReactiveCommand<Unit, BalanceViewModel> BalanceCommand { get; }

	public string BalanceExplanation => "Check your balance.";

	public string HistoryCommandCaption => "📜 History";

	public ReactiveCommand<Unit, HistoryViewModel> HistoryCommand { get; }

	public string HistoryExplanation => "See your transaction history.";

	public bool IsBackupCommandPromoted => this.isBackupCommandPromoted.Value;

	public string BackupCommandCaption => "🗻 Backup your wallet";

	public ReactiveCommand<Unit, BackupViewModel> BackupCommand { get; }

	public string BackupExplanation => "No bank is backing your Zcash for you. If you lose this device, you lose your Zcash. Back up your wallet to protect your Zcash or use it from multiple devices.";
}
