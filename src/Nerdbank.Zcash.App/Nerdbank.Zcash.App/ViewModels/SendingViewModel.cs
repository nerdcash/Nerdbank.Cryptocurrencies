// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash.App.ViewModels;

public class SendingViewModel : ViewModelBase
{
	private string recipientAddress = string.Empty;
	private decimal amount;
	private decimal? fee = 0.0001m; // for design time simulation
	private string memo = string.Empty;

	public SendingViewModel()
	{
		this.SendCommand = ReactiveCommand.Create(() => { });
		this.ScanCommand = ReactiveCommand.Create(() => { });
		this.LinkProperty(nameof(this.Fee), nameof(this.FeeFormatted));
		this.LinkProperty(nameof(this.Amount), nameof(this.Subtotal));
		this.LinkProperty(nameof(this.Subtotal), nameof(this.SubtotalFormatted));
		this.LinkProperty(nameof(this.Subtotal), nameof(this.Total));
		this.LinkProperty(nameof(this.Fee), nameof(this.Total));
	}

	public string Title => "Send Zcash";

	public string RecipientAddressCaption => "Recipient address:";

	public string RecipientAddress
	{
		get => this.recipientAddress;
		set => this.RaiseAndSetIfChanged(ref this.recipientAddress, value);
	}

	public string AmountCaption => "Amount:";

	public string TickerSymbol => this.Network.GetTickerName();

	public decimal Amount
	{
		get => this.amount;
		set => this.RaiseAndSetIfChanged(ref this.amount, value);
	}

	public string FeeCaption => "Fee";

	public decimal? Fee
	{
		get => this.fee;
		set => this.RaiseAndSetIfChanged(ref this.fee, value);
	}

	public ZcashAmountFormatted FeeFormatted => new(this.Fee ?? 0, this.Network);

	public decimal Subtotal => this.Amount;

	public string SubtotalCaption => "Subtotal";

	public ZcashAmountFormatted SubtotalFormatted => new(this.Subtotal, this.Network);

	public string TotalCaption => "Total";

	public decimal Total => this.Amount + (this.Fee ?? 0);

	public ZcashAmountFormatted TotalFormatted => new(this.Total, this.Network);

	public string MemoCaption => "Memo:";

	public string Memo
	{
		get => this.memo;
		set => this.RaiseAndSetIfChanged(ref this.memo, value);
	}

	public string SendCommandCaption => "📤 Send";

	public ReactiveCommand<Unit, Unit> SendCommand { get; }

	public string ScanCommandCaption => "Scan address or payment request";

	public ReactiveCommand<Unit, Unit> ScanCommand { get; }
}
