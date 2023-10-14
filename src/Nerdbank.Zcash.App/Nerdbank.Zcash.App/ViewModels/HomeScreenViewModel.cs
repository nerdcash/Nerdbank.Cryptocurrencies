// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Avalonia.Media.Imaging;

namespace Nerdbank.Zcash.App.ViewModels;

// Consider using +, - and = for receive, spend and balance buttons respectively.
public class HomeScreenViewModel : ViewModelBase
{
	public HomeScreenViewModel()
	{
		this.ReceiveCommand = ReactiveCommand.Create(() => { });
		this.SendCommand = ReactiveCommand.Create(() => { });
		this.BalanceCommand = ReactiveCommand.Create(() => { });
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
}
