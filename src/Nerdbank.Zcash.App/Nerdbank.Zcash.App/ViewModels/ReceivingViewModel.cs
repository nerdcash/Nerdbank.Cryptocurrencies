// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using Nerdbank.QRCodes;
using QRCoder;

namespace Nerdbank.Zcash.App.ViewModels;

public class ReceivingViewModel : ViewModelBase, IDisposable
{
	private readonly IViewModelServicesWithWallet viewModelServices;

	/// <summary>
	/// The unique diversifier index to use for the receiving address if the contact
	/// that is being shown the address doesn't already have one assigned.
	/// </summary>
	private readonly DiversifierIndex newDiversifierIndex;

	/// <summary>
	/// The address index to use for the transparent address if the contact
	/// that is being shown the address doesn't already have one assigned.
	/// </summary>
	private readonly uint newTransparentAddressIndex;

	private string receiverIdentity = string.Empty;
	private ReceivingAddress displayedAddress;

	public ReceivingViewModel()
		: this(new DesignTimeViewModelServices())
	{
	}

	public ReceivingViewModel(IViewModelServicesWithWallet viewModelServices)
	{
		this.viewModelServices = viewModelServices;

		QRCodeGenerator generator = new();
		QREncoder encoder = new() { NoPadding = true };

		ZcashAccount account = viewModelServices.SelectedAccount;
		this.newDiversifierIndex = new(DateTime.UtcNow.Ticks);
		if (account.HasDiversifiableKeys)
		{
			UnifiedAddress unifiedAddress = account.GetDiversifiedAddress(ref this.newDiversifierIndex);
			this.Addresses.Add(new(unifiedAddress, Strings.UnifiedReceivingAddressHeader));

			if (unifiedAddress.GetPoolReceiver<SaplingReceiver>() is { } saplingReceiver)
			{
				SaplingAddress saplingAddress = new(saplingReceiver, unifiedAddress.Network);
				this.Addresses.Add(new(saplingAddress, Strings.SaplingReceivingAddressHeader));
			}
		}

		if (viewModelServices.SelectedAccount.IncomingViewing.Transparent is { } transparent)
		{
			// Consume a fresh transparent address for this receiver.
			// We'll bump the max index up by one if the owner indicates the address was actually 'consumed' by the receiver.
			this.newTransparentAddressIndex = viewModelServices.Wallet.MaxTransparentAddressIndex is uint idx ? idx + 1 : 1;
			TransparentAddress transparentAddress = transparent.GetReceiverIndex(this.newTransparentAddressIndex).DefaultAddress;
			this.Addresses.Add(new(transparentAddress, Strings.TransparentReceivingAddressHeader));
		}

		this.displayedAddress = this.Addresses[0];

		this.AddPaymentRequestCommand = ReactiveCommand.Create(() => { });
	}

	public string Title => "Receive Zcash";

	public string ReceiverIdentityLabel => "Who are you showing this address to?";

	public string ReceiverIdentity
	{
		get => this.receiverIdentity;
		set => this.RaiseAndSetIfChanged(ref this.receiverIdentity, value);
	}

	public ReceivingAddress DisplayedAddress
	{
		get => this.displayedAddress;
		set => this.RaiseAndSetIfChanged(ref this.displayedAddress, value);
	}

	public ObservableCollection<ReceivingAddress> Addresses { get; } = new();

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
	public string PaymentReceivedText => $"You received {this.DisplayedAddress.Address.Network.AsSecurity().Amount(0.1m)} at this address 5 seconds ago";

	public void Dispose()
	{
		foreach (ReceivingAddress address in this.Addresses)
		{
			address.Dispose();
		}

		this.Addresses.Clear();
	}
}
