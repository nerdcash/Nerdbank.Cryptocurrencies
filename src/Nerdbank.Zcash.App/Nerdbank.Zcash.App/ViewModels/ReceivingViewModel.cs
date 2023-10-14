// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Avalonia.Media.Imaging;
using Nerdbank.QRCodes;
using QRCoder;

namespace Nerdbank.Zcash.App.ViewModels;

public class ReceivingViewModel : ViewModelBase
{
	private readonly IViewModelServices viewModelServices;
	private string receiverIdentity = string.Empty;

	public ReceivingViewModel()
		: this(new DesignTimeViewModelServices())
	{
	}

	public ReceivingViewModel(IViewModelServices viewModelServices)
	{
		this.viewModelServices = viewModelServices;

		QRCodeGenerator generator = new();
		QREncoder encoder = new() { NoPadding = true };
		QRCodeData data = generator.CreateQrCode("some data", encoder.ECCLevel);
		this.QrCode = new Bitmap(new MemoryStream(encoder.Encode(data, ".png", null)));

		this.AddPaymentRequestCommand = ReactiveCommand.Create(() => { });
	}

	public string Title => "Receive Zcash";

	public string ReceiverIdentityLabel => "Who are you showing this address to?";

	public string ReceiverIdentity
	{
		get => this.receiverIdentity;
		set => this.RaiseAndSetIfChanged(ref this.receiverIdentity, value);
	}

	public Bitmap QrCode { get; private set; }

	public string Address { get; } = "u1aoeuchch...oechknce";

	public string Explanation => "A unique address is generated every time you share your address with someone. This enhances your privacy and helps you identify where payments come from.";

	public string AddPaymentRequestCaption => "Add expected payment details to the QR code";

	/// <summary>
	/// Gets a value indicating whether the <see cref="AddPaymentRequestCommand"/> button should be visible.
	/// </summary>
	/// <remarks>
	/// The button should disappear after payment is received.
	/// </remarks>
	public bool AddPaymentRequestVisible => true;

	public ReactiveCommand<Unit, Unit> AddPaymentRequestCommand { get; }

	/// <summary>
	/// Gets a message informing the user as to when the last payment was received at this address, if any.
	/// This is specifically to help the user confirm that the sender has actually sent them something, so
	/// as they're waiting, we should show them transactions even from the mempool.
	/// If it has few confirmations, we should show that too.
	/// </summary>
	public string PaymentReceivedText => "You received 0.0001 ZEC at this address 5 seconds ago";
}
