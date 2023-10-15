// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Cryptocurrencies.Exchanges;

namespace Nerdbank.Zcash.App.ViewModels;

public class SendingViewModel : ViewModelBase
{
	private readonly IViewModelServicesWithWallet viewModelServices;
	private string recipientAddress = string.Empty;
	private decimal amount;
	private SecurityAmount? fee;
	private string memo = string.Empty;

	[Obsolete("For design-time use only.", error: true)]
	public SendingViewModel()
		: this(new DesignTimeViewModelServices())
	{
		this.fee = new(0.0001m, this.viewModelServices.SelectedAccount.Network.AsSecurity());
	}

	public SendingViewModel(IViewModelServicesWithWallet viewModelServices)
	{
		this.viewModelServices = viewModelServices;

		this.SendCommand = ReactiveCommand.Create(() => { });
		this.ScanCommand = ReactiveCommand.Create(() => { });
		this.LinkProperty(nameof(this.Amount), nameof(this.Subtotal));
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

	public string TickerSymbol => this.viewModelServices.SelectedAccount.Network.GetTickerName();

	public decimal Amount
	{
		get => this.amount;
		set => this.RaiseAndSetIfChanged(ref this.amount, value);
	}

	public string FeeCaption => "Fee";

	public SecurityAmount? Fee
	{
		get => this.fee;
		set => this.RaiseAndSetIfChanged(ref this.fee, value);
	}

	public SecurityAmount Subtotal => new(this.Amount, this.viewModelServices.SelectedAccount.Network.AsSecurity());

	public string SubtotalCaption => "Subtotal";

	public string TotalCaption => "Total";

	public SecurityAmount Total => new SecurityAmount(this.Amount, this.viewModelServices.SelectedAccount.Network.AsSecurity()) + (this.Fee ?? default);

	public string MemoCaption => "Memo:";

	public string Memo
	{
		get => this.memo;
		set => this.RaiseAndSetIfChanged(ref this.memo, value);
	}

	public string SendCommandCaption => "📤 Send";

	public ReactiveCommand<Unit, Unit> SendCommand { get; private set; }

	public string ScanCommandCaption => "Scan address or payment request";

	public ReactiveCommand<Unit, Unit> ScanCommand { get; private set; }
}
