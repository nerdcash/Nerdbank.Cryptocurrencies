// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;

namespace Nerdbank.Zcash.App.ViewModels;

public class ReceivingIntentSelectorViewModel : ViewModelBaseWithAccountSelector, IHasTitle
{
	private Contact? selectedReceiver;
	private string receiverIdentity = string.Empty;
	private ObservableAsPropertyHelper<string> proceedCaption;

	[Obsolete("Design-time only", error: true)]
	public ReceivingIntentSelectorViewModel()
		: this(new DesignTimeViewModelServices())
	{
	}

	public ReceivingIntentSelectorViewModel(IViewModelServices viewModelServices)
		: base(viewModelServices)
	{
		this.PaymentRequestDetails = new PaymentRequestDetailsViewModel(viewModelServices);

		this.ProceedCommand = ReactiveCommand.Create(this.Proceed);

		this.proceedCaption = this.WhenAnyValue(
			x => x.ReceiverIdentity,
			x => x.PaymentRequestDetails.IsEmpty,
			(receiverIdentity, emptyPaymentRequest) => emptyPaymentRequest ? (receiverIdentity.Length == 0 ? ReceivingIntentSelectorStrings.JustLooking : ReceivingIntentSelectorStrings.ShowMyAddress) : ReceivingIntentSelectorStrings.ShowInvoice)
			.ToProperty(this, nameof(this.ProceedCaption));
	}

	public string Title => ReceivingIntentSelectorStrings.Title;

	public string ReceivingAccountCaption => ReceivingIntentSelectorStrings.ReceivingAccountCaption;

	public string ReceiverIdentityLabel => ReceivingIntentSelectorStrings.ReceiverIdentityLabel;

	public string ReceiverIdentity
	{
		get => this.receiverIdentity;
		set => this.RaiseAndSetIfChanged(ref this.receiverIdentity, value);
	}

	public Contact? SelectedReceiver
	{
		get => this.selectedReceiver;
		set => this.RaiseAndSetIfChanged(ref this.selectedReceiver, value);
	}

	public ReadOnlyObservableCollection<Contact> SuggestedReceivers => this.ViewModelServices.ContactManager.Contacts;

	public string ReceiverExplanation => ReceivingIntentSelectorStrings.ReceiverExplanation;

	public string ProceedCaption => this.proceedCaption.Value;

	public ReactiveCommand<Unit, Unit> ProceedCommand { get; }

	public string PaymentRequestCaption => ReceivingIntentSelectorStrings.PaymentRequestCaption;

	public PaymentRequestDetailsViewModel PaymentRequestDetails { get; }

	public void Proceed()
	{
		if (this.SelectedReceiver is null && this.ReceiverIdentity.Length > 0)
		{
			// The Back button that navigates to this view model will sometimes have a ReceiverIdentity set that would match,
			// but the AutoCompleteBox control hasn't matched it to an existing contact.
			// Search for the contact, and only create a new one if it doesn't exist.
			if (this.ViewModelServices.ContactManager.FindContact(this.ReceiverIdentity) is Contact match)
			{
				this.SelectedReceiver = match;
			}
			else
			{
				// Create a new contact as named.
				this.SelectedReceiver = new Contact
				{
					Name = this.ReceiverIdentity,
				};
				this.ViewModelServices.ContactManager.Add(this.SelectedReceiver);
			}
		}

		ReceivingViewModel receivingViewModel = new(
			this.ViewModelServices,
			this.SelectedAccount,
			this.SelectedReceiver,
			this.PaymentRequestDetails.IsEmpty ? null : this.PaymentRequestDetails);
		this.ViewModelServices.NavigateTo(receivingViewModel);
	}

	protected override void OnSelectedAccountChanged()
	{
		base.OnSelectedAccountChanged();
		this.PaymentRequestDetails.SelectedAccount = this.SelectedAccount;
	}
}
