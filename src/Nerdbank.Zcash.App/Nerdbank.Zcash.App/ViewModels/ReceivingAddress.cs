// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Avalonia.Input;
using Avalonia.Media.Imaging;
using Nerdbank.QRCodes;
using QRCoder;

namespace Nerdbank.Zcash.App.ViewModels;

public class ReceivingAddress : IDisposable
{
	private readonly IViewModelServices viewModelServices;
	private readonly PaymentRequestDetailsViewModel? paymentRequestDetails;

	public ReceivingAddress(IViewModelServices viewModelServices, ZcashAddress address, PaymentRequestDetailsViewModel? paymentRequestDetails, string header)
	{
		this.Header = header;
		if (address.HasShieldedReceiver)
		{
			this.Header = $"{this.Header} 🛡️";
		}

		this.viewModelServices = viewModelServices;
		this.Address = address;
		this.paymentRequestDetails = paymentRequestDetails;
		if (paymentRequestDetails is not null)
		{
			Zip321PaymentRequestUris.PaymentRequest paymentRequest = new(paymentRequestDetails.ToDetails(address));
			this.FullText = paymentRequest.ToString();
			this.ShortText = $"{this.FullText[..30]}...";
		}
		else
		{
			this.FullText = address;
			this.ShortText = $"{address.Address[..15]}...{address.Address[^15..]}";
		}

		try
		{
			this.QRCode = this.CreateQRCode();
		}
		catch (InvalidOperationException)
		{
			// This fails in unit tests because Avalonia Bitmap can't find required UI services.
		}

		bool canCopy = viewModelServices.TopLevel?.Clipboard is not null;
		this.CopyCommand = ReactiveCommand.CreateFromTask(this.CopyAsync, new ObservableBox<bool>(canCopy));
	}

	public string Header { get; }

	public string? Subheading => !this.Address.HasShieldedReceiver ? ReceivingStrings.TransparentAddressSubheading : null;

	public Bitmap? QRCode { get; }

	public ZcashAddress Address { get; }

	public string FullText { get; }

	public string ShortText { get; }

	public string CopyTextPrompt => ReceivingStrings.CopyTextPrompt;

	public ReactiveCommand<Unit, Unit> CopyCommand { get; }

	public async Task CopyAsync()
	{
		if (this.viewModelServices.TopLevel?.Clipboard is null)
		{
			throw new NotSupportedException("No clipboard.");
		}

		DataObject clipboardData = new();

		// We put the raw address or payment request text on the clipboard in case the user pastes it into a text field.
		clipboardData.Set(DataFormats.Text, this.FullText);

		if (this.paymentRequestDetails is not null)
		{
			// We also offer HTML that makes a hyperlink to the payment request.
			// This is because zcash: payment request URIs don't tend to automatically generate hyperlinks
			// in emails and other communication. By creating a hyperlink, and adding an https: redirector
			// in front of it, we greatly increase usability of a shared payment request.
			string html = $"<a href=\"https://zcash.nerdbank.net/paymentrequest#{this.FullText}\">{ReceivingStrings.PayInvoiceHyperlinkText}</a>";
			clipboardData.SetHtml(html);
		}

		try
		{
			await this.viewModelServices.TopLevel.Clipboard.SetDataObjectAsync(clipboardData);
		}
		catch (Exception)
		{
			// Avalonia doesn't support SetDataObjectAsync on all platforms.
			await this.viewModelServices.TopLevel.Clipboard.SetTextAsync(this.FullText);
		}
	}

	public void Dispose()
	{
		this.QRCode?.Dispose();
	}

	private Bitmap CreateQRCode()
	{
		QRCodeGenerator generator = new();
		QREncoder encoder = new() { NoPadding = true };
		QRCodeData data = generator.CreateQrCode(this.FullText, encoder.ECCLevel);
		return new Bitmap(new MemoryStream(encoder.Encode(data, ".png", null)));
	}
}
