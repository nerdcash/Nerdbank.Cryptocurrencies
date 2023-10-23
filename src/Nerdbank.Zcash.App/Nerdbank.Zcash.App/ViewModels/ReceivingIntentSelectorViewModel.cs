// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Zcash.App.Models;

namespace Nerdbank.Zcash.App.ViewModels;

public class ReceivingIntentSelectorViewModel : ViewModelBase
{
	private readonly IViewModelServicesWithSelectedAccount viewModelServices;
	private string receiverIdentity = string.Empty;

	[Obsolete("Design-time only", error: true)]
	public ReceivingIntentSelectorViewModel()
		: this(new DesignTimeViewModelServices())
	{
	}

	public ReceivingIntentSelectorViewModel(IViewModelServicesWithSelectedAccount viewModelServices)
	{
		this.viewModelServices = viewModelServices;

		this.PaymentRequestDetails = new PaymentRequestDetailsViewModel(viewModelServices.SelectedAccount.Network.AsSecurity());

		this.ProceedCommand = ReactiveCommand.Create(this.Proceed);
		this.LinkProperty(nameof(this.ReceiverIdentity), nameof(this.ProceedCaption));
	}

	public string Title => "Receive Zcash";

	public string ReceiverIdentityLabel => "Who are you sharing your address with?";

	public string ReceiverIdentity
	{
		get => this.receiverIdentity;
		set => this.RaiseAndSetIfChanged(ref this.receiverIdentity, value);
	}

	public string ReceiverExplanation => "A unique address is generated every time you share your address with someone. This enhances your privacy and helps you identify where payments come from.";

	public string ProceedCaption => this.ReceiverIdentity.Length == 0 ? "I'm just looking" : "Show my address";

	public ReactiveCommand<Unit, Unit> ProceedCommand { get; }

	public string PaymentRequestCaption => "Include payment request";

	public PaymentRequestDetailsViewModel PaymentRequestDetails { get; }

	public void Proceed()
	{
		Contact? matchingContact = null;
		ReceivingViewModel receivingViewModel = new(
			this.viewModelServices,
			matchingContact,
			this.PaymentRequestDetails.IsEmpty ? null : this.PaymentRequestDetails);
		this.viewModelServices.NavigateTo(receivingViewModel);
	}
}
