// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Avalonia.Media.Imaging;

namespace Nerdbank.Zcash.App.ViewModels;

// Consider using +, - and = for receive, spend and balance buttons respectively.
public class HomeScreenViewModel : ViewModelBase
{
	private bool isBackupRecommended;

	public HomeScreenViewModel()
	{
		this.ReceiveCommand = ReactiveCommand.Create(() => { });
		this.SendCommand = ReactiveCommand.Create(() => { });
		this.BalanceCommand = ReactiveCommand.Create(() => { });
		this.BackupCommand = ReactiveCommand.Create(() => { });

		// TODO: determine if backup is recommended based on whether the user checked off that task in the Backup view.
		this.isBackupRecommended = true;
	}

	public Bitmap Logo => Resources.ZcashLogo;

	public string ReceiveCommandCaption => "Receive";

	public ReactiveCommand<Unit, Unit> ReceiveCommand { get; }

	public string ReceiveExplanation => "Help someone send you Zcash by sharing your address with them.";

	public string SendCommandCaption => "Send";

	public ReactiveCommand<Unit, Unit> SendCommand { get; }

	public string SendExplanation => "Send someone Zcash.";

	public string BalanceCommandCaption => "Balance";

	public ReactiveCommand<Unit, Unit> BalanceCommand { get; }

	public string BalanceExplanation => "Check your balance.";

	public bool IsBackupRecommended
	{
		get => this.isBackupRecommended;
		set => this.RaiseAndSetIfChanged(ref this.isBackupRecommended, value);
	}

	public string BackupCommandCaption => "Backup your wallet";

	public ReactiveCommand<Unit, Unit> BackupCommand { get; }

	public string BackupExplanation => "No bank is backing your Zcash for you. If you lose this device, you lose your Zcash. Back up your wallet to protect your Zcash or use it from multiple devices.";
}
