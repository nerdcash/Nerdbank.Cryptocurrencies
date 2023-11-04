// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel.DataAnnotations;
using Nerdbank.Cryptocurrencies.Exchanges;
using ZXing.Mobile;

namespace Nerdbank.Zcash.App.ViewModels;

public class SendingViewModel : ViewModelBaseWithAccountSelector, IHasTitle
{
	private readonly ObservableAsPropertyHelper<string> tickerSymbol;
	private readonly ObservableAsPropertyHelper<SecurityAmount> subtotal;
	private readonly ObservableAsPropertyHelper<SecurityAmount> total;
	private string recipientAddress = string.Empty;
	private decimal amount;
	private SecurityAmount? fee;
	private string memo = string.Empty;

	[Obsolete("For design-time use only.", error: true)]
	public SendingViewModel()
		: this(new DesignTimeViewModelServices())
	{
		this.fee = this.SelectedAccount is not null ? new(0.0001m, this.SelectedAccount.Network.AsSecurity()) : null;
	}

	public SendingViewModel(IViewModelServices viewModelServices)
		: base(viewModelServices)
	{
		this.tickerSymbol = this.WhenAnyValue(
			vm => vm.SelectedAccount,
			a => a?.Network.GetTickerName() ?? UnknownSecurity.TickerSymbol).ToProperty(this, nameof(this.TickerSymbol));
		this.subtotal = this.WhenAnyValue(
			vm => vm.Amount,
			vm => vm.SelectedAccount,
			(amount, account) => new SecurityAmount(amount, account?.ZcashAccount.Network.AsSecurity() ?? UnknownSecurity))
			.ToProperty(this, nameof(this.Subtotal));
		this.total = this.WhenAnyValue(
			vm => vm.Subtotal,
			vm => vm.Fee,
			(subtotal, fee) => fee is null ? subtotal : subtotal + fee.Value)
			.ToProperty(this, nameof(this.Total));

		this.SendCommand = ReactiveCommand.Create(() => { });
		this.ScanCommand = ReactiveCommand.CreateFromTask(this.ScanAsync);
		this.LinkProperty(nameof(this.Amount), nameof(this.Subtotal));
		this.LinkProperty(nameof(this.Subtotal), nameof(this.Total));
		this.LinkProperty(nameof(this.Fee), nameof(this.Total));
	}

	public string Title => "Send Zcash";

	public string FromAccountCaption => "From account:";

	public string RecipientAddressCaption => "Recipient address:";

	[Required(ErrorMessage = ""), ZcashAddress]
	public string RecipientAddress
	{
		get => this.recipientAddress;
		set => this.RaiseAndSetIfChanged(ref this.recipientAddress, value);
	}

	public string AmountCaption => "Amount:";

	public string TickerSymbol => this.tickerSymbol.Value;

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

	public SecurityAmount Subtotal => this.subtotal.Value;

	public string SubtotalCaption => "Subtotal";

	public string TotalCaption => "Total";

	public SecurityAmount Total => this.total.Value;

	public string MemoCaption => "Memo:";

	public string Memo
	{
		get => this.memo;
		set => this.RaiseAndSetIfChanged(ref this.memo, value);
	}

	public string SendCommandCaption => "📤 Send";

	public ReactiveCommand<Unit, Unit> SendCommand { get; private set; }

	public ReactiveCommand<Unit, Unit> ScanCommand { get; private set; }

	private async Task ScanAsync()
	{
		try
		{
			MobileBarcodeScanner scanner = new();
			MobileBarcodeScanningOptions options = new()
			{
			};
			ZXing.Result result = await scanner.Scan(options);
		}
		catch (NotSupportedException)
		{
			// fallback to file picker
			// TODO
		}
	}
}
