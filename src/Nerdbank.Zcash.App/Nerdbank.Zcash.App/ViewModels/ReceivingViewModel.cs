// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using Microsoft;
using Nerdbank.QRCodes;
using QRCoder;

namespace Nerdbank.Zcash.App.ViewModels;

public class ReceivingViewModel : ViewModelBase, IDisposable, IHasTitle
{
	private readonly IViewModelServices viewModelServices;
	private readonly Contact? observingContact;
	private readonly Contact.AssignedSendingAddresses? assignedAddresses;
	private readonly uint transparentAddressIndex;
	private readonly Account receivingAccount;

	private ReceivingAddress? displayedAddress;

	[Obsolete("Design-time only", error: true)]
	public ReceivingViewModel()
		: this(new DesignTimeViewModelServices(), null, null, null)
	{
	}

	public ReceivingViewModel(
		IViewModelServices viewModelServices,
		Account? receivingAccount,
		Contact? observingContact,
		PaymentRequestDetailsViewModel? paymentRequestDetailsViewModel)
	{
		this.viewModelServices = viewModelServices;
		this.observingContact = observingContact;

		this.receivingAccount = receivingAccount ?? viewModelServices.Wallet.Accounts.First();

		this.SyncProgress = new SyncProgressData(this.viewModelServices) { Account = this.receivingAccount };

		this.assignedAddresses = observingContact?.GetOrCreateSendingAddressAssignment(this.receivingAccount);
		if (this.receivingAccount.ZcashAccount.HasDiversifiableKeys)
		{
			DiversifierIndex diversifierIndex = this.assignedAddresses?.Diversifier ?? new(DateTime.UtcNow.Ticks);
			UnifiedAddress unifiedAddress = this.receivingAccount.ZcashAccount.GetDiversifiedAddress(ref diversifierIndex);
			this.Addresses.Add(new(viewModelServices, unifiedAddress, paymentRequestDetailsViewModel, Strings.UnifiedReceivingAddressHeader));

			if (unifiedAddress.GetPoolReceiver<SaplingReceiver>() is { } saplingReceiver)
			{
				SaplingAddress saplingAddress = new(saplingReceiver, unifiedAddress.Network);
				this.Addresses.Add(new(viewModelServices, saplingAddress, paymentRequestDetailsViewModel, Strings.SaplingReceivingAddressHeader));
			}
		}

		if (this.receivingAccount.ZcashAccount.IncomingViewing.Transparent is { } transparent)
		{
			// Consume a fresh transparent address for this receiver.
			// We'll bump the max index up by one if the owner indicates the address was actually 'consumed' by the receiver.
			this.transparentAddressIndex = this.assignedAddresses?.TransparentAddressIndex ?? (this.receivingAccount.ZcashAccount.MaxTransparentAddressIndex is uint idx ? idx + 1 : 1);
			TransparentAddress transparentAddress = this.receivingAccount.ZcashAccount.GetTransparentAddress(this.transparentAddressIndex);
			this.Addresses.Add(new(viewModelServices, transparentAddress, paymentRequestDetailsViewModel, Strings.TransparentReceivingAddressHeader));
		}

		this.IsTestNetWarningVisible = this.receivingAccount.Network != ZcashNetwork.MainNet;

		this.displayedAddress = this.Addresses[0];
		this.RecordTransparentAddressShownIfApplicable();
	}

	public string Title => "Receive Zcash";

	public SyncProgressData SyncProgress { get; }

	public string AccountName => this.receivingAccount.Name;

	public string TestNetWarning => Strings.TestNetIsWorthlessWarning;

	public bool IsTestNetWarningVisible { get; }

	public bool AddressAssignedToContactIsVisible => this.observingContact is not null;

	public string AddressAssignedToContactExplanation => $"This receiving address is only for **{this.observingContact?.Name}**.";

	public ReceivingAddress? DisplayedAddress
	{
		get => this.displayedAddress;
		set
		{
			this.RaiseAndSetIfChanged(ref this.displayedAddress, value);
			this.RecordTransparentAddressShownIfApplicable();
		}
	}

	public ObservableCollection<ReceivingAddress> Addresses { get; } = new();

	public string AddPaymentRequestCaption => "Payment details";

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

	private void RecordTransparentAddressShownIfApplicable()
	{
		if (this.DisplayedAddress?.Address is TransparentAddress && this.assignedAddresses is not null)
		{
			this.assignedAddresses.TransparentAddressIndex ??= this.transparentAddressIndex;
			if (this.transparentAddressIndex > this.receivingAccount.ZcashAccount.MaxTransparentAddressIndex || this.receivingAccount.ZcashAccount.MaxTransparentAddressIndex is null)
			{
				this.receivingAccount.ZcashAccount.MaxTransparentAddressIndex = this.transparentAddressIndex;
			}
		}
	}
}
