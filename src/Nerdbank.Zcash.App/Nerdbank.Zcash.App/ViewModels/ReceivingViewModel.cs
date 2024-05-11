// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Globalization;
using System.Reactive.Linq;
using System.Text;
using DynamicData.Binding;
using Microsoft.VisualStudio.Threading;
using Nerdbank.Cryptocurrencies;

namespace Nerdbank.Zcash.App.ViewModels;

public class ReceivingViewModel : ViewModelBase, IDisposable, IHasTitle
{
	private readonly IViewModelServices viewModelServices;
	private readonly Contact? observingContact;
	private readonly Contact.AssignedSendingAddresses? assignedAddresses;
	private readonly uint transparentAddressIndex;
	private readonly Account receivingAccount;
	private readonly CancellationTokenSource disposalTokenSource = new();
	private readonly ZcashAddress unifiedWithTransparent;
	private string? paymentReceivedText;
	private ObservableAsPropertyHelper<ZcashTransaction?> lastTransactionReceived;

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
		List<ZcashAddress> rawReceiverAddresses = new();
		if (this.receivingAccount.ZcashAccount.HasDiversifiableKeys)
		{
			DiversifierIndex diversifierIndex = this.assignedAddresses?.Diversifier ?? new(DateTime.UtcNow.Ticks);
			UnifiedAddress unifiedAddress = this.receivingAccount.ZcashAccount.GetDiversifiedAddress(ref diversifierIndex);
			this.Addresses.Add(new(viewModelServices, unifiedAddress, paymentRequestDetailsViewModel, Strings.UnifiedReceivingAddressHeader));
			rawReceiverAddresses.AddRange(unifiedAddress.Receivers);

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
			rawReceiverAddresses.Add(transparentAddress);
		}

		this.unifiedWithTransparent = UnifiedAddress.Create(rawReceiverAddresses);

		this.IsTestNetWarningVisible = this.receivingAccount.Network != ZcashNetwork.MainNet;

		// Watch for incoming transactions.
		this.lastTransactionReceived = this.receivingAccount.Transactions.ObserveCollectionChanges()
			.Select(_ => this.FindLastReceivedTransaction())
			.ToProperty(this, nameof(this.LastTransactionReceived), initialValue: this.FindLastReceivedTransaction());
		this.KeepLastTransactionMessageFreshAsync().Forget();

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
	public string? PaymentReceivedText
	{
		get => this.paymentReceivedText;
		private set => this.RaiseAndSetIfChanged(ref this.paymentReceivedText, value);
	}

	public ZcashTransaction? LastTransactionReceived => this.lastTransactionReceived.Value;

	private Security SelectedSecurity => this.receivingAccount.Network.AsSecurity();

	public void Dispose()
	{
		this.disposalTokenSource.Cancel();
		this.disposalTokenSource.Dispose();

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
			this.receivingAccount.RecordDisplayedTransparentAddress(this.transparentAddressIndex);
		}
	}

	private ZcashTransaction? FindLastReceivedTransaction()
	{
		// When we report the amount sent, we want to filter it down to just what was sent to the address(es) actually displayed by this view.
		// This informs the user when the person they are showing the address to has actually sent them something, rather than misinform them
		// when a payment came in from someone else.
		return this.receivingAccount.Transactions
			.Where(t => t.IsIncoming && t.GetAmountReceivedUsingAddress(this.unifiedWithTransparent) > 0)
			.OrderByDescending(t => t.When)
			.Take(1)
			.FirstOrDefault();
	}

	private string? ComputeLastTransactionReceivedTime(ZcashTransaction? lastTransaction)
	{
		if (lastTransaction is null)
		{
			return null;
		}

		StringBuilder builder = new();

		// When we report the amount sent, we want to filter it down to just what was sent to the address(es) actually displayed by this view.
		SecurityAmount amount = this.SelectedSecurity.Amount(lastTransaction.GetAmountReceivedUsingAddress(this.unifiedWithTransparent));
		builder.Append(CultureInfo.CurrentCulture, $"You received {amount} at this address");

		TimeSpan? age = DateTime.UtcNow - lastTransaction.When;
		if (age is not null)
		{
			builder.Append(CultureInfo.CurrentCulture, $" {AppUtilities.FriendlyTimeSpan(age.Value)}");
		}

		uint confirmations = (this.receivingAccount.LastBlockHeight - lastTransaction.BlockNumber) + 1 ?? 0;
		if (confirmations > 0)
		{
			builder.Append(CultureInfo.CurrentCulture, $", confirmed {confirmations} times.");
		}
		else
		{
			builder.Append(" (unconfirmed).");
		}

		return builder.ToString();
	}

	private async Task KeepLastTransactionMessageFreshAsync()
	{
		while (!this.disposalTokenSource.IsCancellationRequested)
		{
			this.PaymentReceivedText = this.ComputeLastTransactionReceivedTime(this.LastTransactionReceived);
			await Task.Delay(1000, this.disposalTokenSource.Token);
		}
	}
}
