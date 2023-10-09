// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash.App.ViewModels;

public class MainViewModel : ViewModelBase
{
	public MainViewModel()
	{
		this.ReceiveCommand = ReactiveCommand.Create(() => { });
		this.SendCommand = ReactiveCommand.Create(() => { });
		this.BalanceCommand = ReactiveCommand.Create(() => { });
	}

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
