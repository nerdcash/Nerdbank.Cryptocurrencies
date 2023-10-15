// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using Nerdbank.QRCodes;
using QRCoder;

namespace Nerdbank.Zcash.App.ViewModels;

public class ReceivingAddress : IDisposable
{
	public ReceivingAddress(ZcashAddress address, string header)
	{
		this.Header = header;
		this.Address = address;

		QRCodeGenerator generator = new();
		QREncoder encoder = new() { NoPadding = true };
		QRCodeData data = generator.CreateQrCode(address, encoder.ECCLevel);
		this.QRCode = new Bitmap(new MemoryStream(encoder.Encode(data, ".png", null)));
	}

	public string Header { get; }

	public Bitmap QRCode { get; }

	public ZcashAddress Address { get; }

	public string AbbreviatedAddress => $"{this.Address.Address[..10]}...{this.Address.Address[^10..]}";

	public string Note => this.Address.GetPoolReceiver<TransparentP2PKHReceiver>() is not null || this.Address.GetPoolReceiver<TransparentP2SHReceiver>() is not null
		? "If you share this address, be sure to commit it to a particular contact so this address isn't reused for anyone else. This enhances your privacy."
		: "A unique address is generated every time you share your address with someone. This enhances your privacy and helps you identify where payments come from.";

	public void Dispose()
	{
		this.QRCode.Dispose();
	}
}
