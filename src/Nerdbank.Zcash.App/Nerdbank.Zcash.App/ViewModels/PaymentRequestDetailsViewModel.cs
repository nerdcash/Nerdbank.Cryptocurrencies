// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash.App.ViewModels;

public class PaymentRequestDetailsViewModel : ViewModelBaseWithAccountSelector
{
	private readonly ObservableAsPropertyHelper<bool> isEmpty;
	private string label = string.Empty;
	private string memo = string.Empty;
	private string message = string.Empty;

	public PaymentRequestDetailsViewModel(IViewModelServices viewModelServices)
		: base(viewModelServices)
	{
		this.AmountEntry = new DualAmountEntryViewModel(viewModelServices);

		this.isEmpty = this.WhenAnyValue(
			x => x.Label,
			x => x.Memo,
			x => x.Message,
			x => x.AmountEntry.Amount,
			(label, memo, message, amount) => label.Length == 0 && memo.Length == 0 && message.Length == 0 && amount is null or 0m)
			.ToProperty(this, nameof(this.IsEmpty));
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

	public string AmountCaption => $"Invoice amount:";

	public DualAmountEntryViewModel AmountEntry { get; }

	public bool IsEmpty => this.isEmpty.Value;

	public Zip321PaymentRequestUris.PaymentRequestDetails ToDetails(ZcashAddress recipient)
	{
		return new(recipient)
		{
			Amount = this.AmountEntry.Amount,
			Label = this.Label,
			Memo = Zcash.Memo.FromMessage(this.Memo),
			Message = this.Message,
		};
	}

	protected override void OnSelectedAccountChanged()
	{
		base.OnSelectedAccountChanged();
		this.AmountEntry.SelectedAccount = this.SelectedAccount;
	}
}
