// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Cryptocurrencies.Exchanges;

namespace Nerdbank.Zcash.App.ViewModels;

public class PaymentRequestDetailsViewModel : ViewModelBase
{
	private readonly Security security;
	private string label = string.Empty;
	private string memo = string.Empty;
	private string message = string.Empty;
	private decimal? amount;

	public PaymentRequestDetailsViewModel(Security security)
	{
		this.security = security;

		this.LinkProperty(nameof(this.Label), nameof(this.IsEmpty));
		this.LinkProperty(nameof(this.Message), nameof(this.IsEmpty));
		this.LinkProperty(nameof(this.Amount), nameof(this.IsEmpty));
		this.LinkProperty(nameof(this.Memo), nameof(this.IsEmpty));
	}

	public string LabelCaption => "Your name:";

	/// <summary>
	/// Gets or sets the label for an address (e.g. name of receiver).
	/// </summary>
	/// <remarks>
	/// If a label is present, a client rendering a payment for inspection by the user SHOULD
	/// display this label (if possible) as well as the associated address.
	/// If the label is displayed, it MUST be identifiable as distinct from the address.
	/// </remarks>
	public string Label
	{
		get => this.label;
		set => this.RaiseAndSetIfChanged(ref this.label, value);
	}

	public string MemoCaption => "Memo suggestion:";

	/// <summary>
	/// Gets or sets or sets the memo that may be required to include in the payment.
	/// </summary>
	public string Memo
	{
		get => this.memo;
		set => this.RaiseAndSetIfChanged(ref this.memo, value);
	}

	public string MessageCaption => "Message:";

	/// <summary>
	/// Gets or sets the message to display from the requester to the payer.
	/// </summary>
	public string Message
	{
		get => this.message;
		set => this.RaiseAndSetIfChanged(ref this.message, value);
	}

	public string AmountCaption => $"Invoice amount (in {this.security.TickerSymbol}):";

	/// <summary>
	/// Gets or sets the requested amount (in ZEC).
	/// </summary>
	public decimal? Amount
	{
		get => this.amount;
		set => this.RaiseAndSetIfChanged(ref this.amount, value);
	}

	public bool IsEmpty => this.Label.Length == 0 && this.Memo.Length == 0 && this.Message.Length == 0 && this.Amount is null or 0m;

	public Zip321PaymentRequestUris.PaymentRequestDetails ToDetails(ZcashAddress recipient)
	{
		return new(recipient)
		{
			Amount = this.Amount,
			Label = this.Label,
			Memo = Zcash.Memo.FromMessage(this.Memo),
			Message = this.Message,
		};
	}
}
