// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Reactive.Linq;
using DynamicData;
using DynamicData.Binding;
using Nerdbank.Cryptocurrencies;
using Nerdbank.Cryptocurrencies.Exchanges;
using ZXing.Mobile;

namespace Nerdbank.Zcash.App.ViewModels;

public class SendingViewModel : ViewModelBaseWithExchangeRate, IHasTitle
{
	private readonly ObservableCollection<LineItem> lineItems = new();
	private readonly ReadOnlyObservableCollection<LineItem> lineItemsReadOnly;
	private readonly ObservableAsPropertyHelper<SecurityAmount?> subtotalAlternate;
	private readonly ObservableAsPropertyHelper<SecurityAmount?> feeAlternate;
	private readonly ObservableAsPropertyHelper<SecurityAmount?> totalAlternate;
	private readonly ObservableAsPropertyHelper<bool> isTestNetWarningVisible;
	private readonly ObservableAsPropertyHelper<bool> isSendingInProgress;
	private bool areLineItemsValid;
	private string? errorMessage;
	private string? sendSuccessfulMessage;
	private string mutableMemo = string.Empty;
	private SecurityAmount subtotal;
	private SecurityAmount? total;
	private ReadOnlyObservableCollection<object>? possibleRecipients;
	private SecurityAmount? fee;
	private IDisposable? possibleRecipientsSubscription;

	[Obsolete("For design-time use only.", error: true)]
	public SendingViewModel()
		: this(new DesignTimeViewModelServices())
	{
		this.LineItems[0].AmountEntry.Amount = 1.23m;
		this.LineItems[0].RecipientLabel = "Best Buy";
		this.LineItems[0].Message = "Thank you for your purchase!";
		this.fee = new(0.0001m, this.SelectedAccount?.Network.AsSecurity() ?? UnknownSecurity);
	}

	public SendingViewModel(IViewModelServices viewModelServices)
		: base(viewModelServices, showOnlyAccountsWithSpendKeys: true)
	{
		this.lineItemsReadOnly = new(this.lineItems);
		this.AddLineItem();

		this.SyncProgress = new SyncProgressData(this);

		this.OnSelectedAccountChanged();

		this.subtotalAlternate = this.WhenAnyValue(
			vm => vm.Subtotal,
			vm => vm.ExchangeRate,
			(subtotal, rate) => ConvertOrNull(rate, subtotal))
			.ToProperty(this, nameof(this.SubtotalAlternate));
		this.feeAlternate = this.WhenAnyValue(
			vm => vm.Fee,
			vm => vm.ExchangeRate,
			(fee, rate) => ConvertOrNull(rate, fee))
			.ToProperty(this, nameof(this.FeeAlternate));
		this.totalAlternate = this.WhenAnyValue(
			vm => vm.Total,
			vm => vm.ExchangeRate,
			(total, rate) => ConvertOrNull(rate, total))
			.ToProperty(this, nameof(this.TotalAlternate));

		this.isTestNetWarningVisible = this.WhenAnyValue(vm => vm.SelectedAccount)
			.Select(account => account?.Network != ZcashNetwork.MainNet)
			.ToProperty(this, nameof(this.IsTestNetWarningVisible));

		// ZingoLib does not support sending more than one transaction at once.
		this.isSendingInProgress = this.WhenAnyValue(vm => vm.SelectedAccount!.SendProgress.IsInProgress)
			.ToProperty(this, nameof(this.IsSendingInProgress));
		IObservable<bool> canSend = this.WhenAnyValue(
			vm => vm.SelectedAccount!.SendProgress.IsInProgress,
			vm => vm.SelectedAccount!.Balance.Spendable,
			vm => vm.Total,
			vm => vm.AreLineItemsValid,
			vm => vm.HasAnyErrors,
			(sending, spendableBalance, total, valid, hasAnyErrors) => valid && !hasAnyErrors && !sending && spendableBalance.Amount >= total?.Amount);

		this.SendCommand = ReactiveCommand.CreateFromTask(this.SendAsync, canSend);
		this.AddLineItemCommand = ReactiveCommand.Create(this.AddLineItem);
	}

	public bool IsSendingInProgress => this.isSendingInProgress.Value;

	public string Title => SendingStrings.Title;

	public SyncProgressData SyncProgress { get; }

	public bool AreLineItemsValid
	{
		get => this.areLineItemsValid;
		private set => this.RaiseAndSetIfChanged(ref this.areLineItemsValid, value);
	}

	public string FromAccountCaption => SendingStrings.FromAccountCaption;

	public string TestNetWarning => Strings.TestNetIsWorthlessWarning;

	public bool IsTestNetWarningVisible => this.isTestNetWarningVisible.Value;

	public string MutableMemoCaption => SendingStrings.MutableMemoCaption;

	public string MutableMemo
	{
		get => this.mutableMemo;
		set => this.RaiseAndSetIfChanged(ref this.mutableMemo, value);
	}

	public ReadOnlyObservableCollection<LineItem> LineItems => this.lineItemsReadOnly;

	public string AddLineItemCommandCaption => SendingStrings.AddLineItemCommandCaption;

	public ReactiveCommand<Unit, LineItem> AddLineItemCommand { get; }

	public string FeeCaption => SendingStrings.FeeCaption;

	public SecurityAmount? Fee
	{
		get => this.fee;
		private set => this.RaiseAndSetIfChanged(ref this.fee, value);
	}

	public SecurityAmount? FeeAlternate => this.feeAlternate.Value;

	public string SubtotalCaption => SendingStrings.SubtotalCaption;

	public SecurityAmount Subtotal
	{
		get => this.subtotal;
		private set => this.RaiseAndSetIfChanged(ref this.subtotal, value);
	}

	public SecurityAmount? SubtotalAlternate => this.subtotalAlternate.Value;

	public string TotalCaption => SendingStrings.TotalCaption;

	public SecurityAmount? Total
	{
		get => this.total;
		private set => this.RaiseAndSetIfChanged(ref this.total, value);
	}

	public SecurityAmount? TotalAlternate => this.totalAlternate.Value;

	public string SendCommandCaption => SendingStrings.SendCommandCaption;

	public ReactiveCommand<Unit, Unit> SendCommand { get; }

	public string? ErrorMessage
	{
		get => this.errorMessage;
		set => this.RaiseAndSetIfChanged(ref this.errorMessage, value);
	}

	public string? SendSuccessfulMessage
	{
		get => this.sendSuccessfulMessage;
		set => this.RaiseAndSetIfChanged(ref this.sendSuccessfulMessage, value);
	}

	public bool TryApplyPaymentRequest(Uri paymentRequestUri)
	{
		if (!Zip321PaymentRequestUris.PaymentRequest.TryParse(paymentRequestUri.OriginalString, out Zip321PaymentRequestUris.PaymentRequest? paymentRequest))
		{
			return false;
		}

		return this.TryApplyPaymentRequest(paymentRequest);
	}

	public bool TryApplyPaymentRequest(Zip321PaymentRequestUris.PaymentRequest paymentRequest)
	{
		if (this.Accounts.Count == 0)
		{
			// No accounts to send from.
			return false;
		}

		this.Clear(leaveOneEmptyLineItem: false);

		ZcashNetwork? network = null;
		foreach (Zip321PaymentRequestUris.PaymentRequestDetails payment in paymentRequest.Payments)
		{
			if (network is null)
			{
				network = payment.Address.Network;
			}
			else if (network != payment.Address.Network)
			{
				// Inconsistent networks in payment request.
				return false;
			}

			LineItem lineItem = this.AddLineItem();
			lineItem.RecipientAddress = payment.Address;
			lineItem.IsRecipientLocked = true;
			lineItem.Amount = payment.Amount;
			lineItem.Memo = payment.Memo.Message ?? string.Empty;
			lineItem.Message = payment.Message;
			lineItem.RecipientLabel = payment.Label;
		}

		// Ensure the selected sending account is not incompatible with the recipients in the payment request.
		if (this.SelectedAccount is not null && this.SelectedAccount.Network != network)
		{
			Account[] compatibleAccounts = this.Accounts.Where(a => a.Network == network).Take(2).ToArray();
			switch (compatibleAccounts.Length)
			{
				case 0:
					this.SelectedAccount = null;

					// Display an error to the user to indicate that this payment goes to a network for which they have no accounts.
					//// TODO: code here
					break;
				case 1:
					this.SelectedAccount = compatibleAccounts[0];
					break;
				default:
					this.SelectedAccount = null;
					break;
			}
		}

		return true;
	}

	protected override void OnSelectedAccountChanged()
	{
		this.RefreshRecipientsList();
		this.RecalculateAggregates();

		foreach (LineItem lineItem in this.lineItems)
		{
			lineItem.AmountEntry.SelectedAccount = lineItem.SelectedAccount = this.SelectedAccount;
		}
	}

	private static SecurityAmount? ConvertOrNull(ExchangeRate? rate, SecurityAmount? amount)
		=> rate is not null && amount is not null && Describes(rate.Value, amount.Value.Security) ? amount * rate : null;

	/// <summary>
	/// Gets a value indicating whether this exchange rate describes a given security.
	/// </summary>
	/// <param name="rate">The exchange rate.</param>
	/// <param name="security">The security in question.</param>
	/// <returns><see langword="true"/> if either security described by this exchange rate is the given <paramref name="security"/>; otherwise <see langword="false" />.</returns>
	private static bool Describes(ExchangeRate rate, Security security) => rate.Basis.Security == security || rate.TradeInterest.Security == security;

	private void RefreshRecipientsList()
	{
		this.possibleRecipientsSubscription?.Dispose();

		// Go ahead and 'capture' the current value of SelectedAccount, since it may change
		// but the filters we're setting up below should not change till we re-execute this code.
		Account? selectedAccount = this.SelectedAccount;

		// The user should be able to select one of their own accounts to send to.
		IObservable<IChangeSet<object>> accounts = this.ViewModelServices.Wallet.Accounts.AsObservableChangeSet()
			.Filter(a => a != selectedAccount && a.Network == selectedAccount?.Network)
			.Transform(a => a as object);

		// Prepare the allowed recipients list from the address book, filtered to those with
		// receiving addresses on the same network.
		IObservable<IChangeSet<object>> contacts = this.ViewModelServices.ContactManager.Contacts.AsObservableChangeSet()
			.Filter(c => c.ReceivingAddresses.Any(addr => addr.Network == selectedAccount?.Network))
			.Transform(c => c as object);

		this.possibleRecipientsSubscription = accounts.Merge(contacts)
			.Bind(out this.possibleRecipients)
			.Subscribe();
		this.RaisePropertyChangedOnLineItems(nameof(LineItem.PossibleRecipients));
	}

	private async Task SendAsync(CancellationToken cancellationToken)
	{
		Verify.Operation(this.SelectedAccount?.LightWalletClient is not null, "No lightclient.");
		Verify.Operation(!this.HasAnyErrors, "Validation errors exist.");
		ImmutableArray<Transaction.LineItem> lineItems = this.GetLineItems();

		// Simulate the payment to get the fee.
		LightWalletClient.SpendDetails sendDetails = this.SelectedAccount.LightWalletClient.SimulateSend(this.SelectedAccount.ZcashAccount, lineItems);

		// Create a draft transaction in the account right away.
		// This will store the mutable memo, exchange rate, and other metadata that
		// isn't going to come back from the light wallet server.
		// In the event of an aborted send, or an expired transaction,
		// the user can use this draft transaction to try sending again later.
		ZcashTransaction tx = new()
		{
			TransactionId = ZcashTransaction.ProvisionalTransactionId,
			MutableMemo = this.MutableMemo,
			IsIncoming = false,
			When = DateTimeOffset.UtcNow,
			SendItems = [.. lineItems.Select(li => new ZcashTransaction.LineItem(li))],
			Fee = sendDetails.Fee,
		};

		// Record the exchange rate that we showed the user, if applicable.
		if (this.ExchangeRate.HasValue)
		{
			this.ViewModelServices.ExchangeData.SetExchangeRate(tx.When.Value, this.ExchangeRate.Value);
		}

		this.SelectedAccount.AddProvisionalTransaction(tx);

		Task sendTask = SendCriticalHelperAsync();
		this.ViewModelServices.RegisterSendTransactionTask(sendTask);
		try
		{
			await sendTask;
			SecurityAmount subtotal = this.Subtotal;

			// Clear the form for the next send.
			this.Clear(leaveOneEmptyLineItem: true);

			// Display a successful message momentarily.
			this.SendSuccessfulMessage = $"{subtotal} sent successfully.";
			await Task.Delay(3000);
			this.SendSuccessfulMessage = null;
		}
		catch (Exception ex)
		{
			this.ErrorMessage = ex.Message;
			this.SelectedAccount.RemoveTransaction(tx);
		}

		// If the user closes the main window, the window will hide but the process will run
		// until this method completes.
		async Task SendCriticalHelperAsync()
		{
			// This assignment helps ensure that we promote the right provisional transaction
			// at the conclusion of the send operation.
			try
			{
				this.ErrorMessage = null;
				ReadOnlyMemory<TxId> transactions = await this.SelectedAccount.LightWalletClient.SendAsync(
					this.SelectedAccount.ZcashAccount,
					lineItems,
					new Progress<LightWalletClient.SendProgress>(this.SelectedAccount.SendProgress.Apply),
					cancellationToken);

				// Semantically, the most similar transaction to what the user intended will be the last one.
				tx.TransactionId = transactions.Span[^1];
			}
			finally
			{
				this.SelectedAccount.SendProgress.Complete();
			}
		}
	}

	private ImmutableArray<Transaction.LineItem> GetLineItems()
	{
		return [..
			from li in this.LineItems
			let to = li.RecipientAddressParsed
			where to is not null
			select new Transaction.LineItem(to, li.Amount ?? 0m, to.HasShieldedReceiver ? Memo.FromMessage(li.Memo) : Memo.NoMemo)];
	}

	private void Clear(bool leaveOneEmptyLineItem)
	{
		this.MutableMemo = string.Empty;
		this.lineItems.Clear();
		if (leaveOneEmptyLineItem)
		{
			this.AddLineItem();
		}

		this.RecalculateAggregates();
		this.UpdateAreLineItemsValidProperty();
	}

	private void RaisePropertyChangedOnLineItems(string propertyName)
	{
		foreach (LineItem lineItem in this.lineItems)
		{
			lineItem.RaisePropertyChanged(propertyName);
		}
	}

	private void LineItem_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
	{
		switch (e.PropertyName)
		{
			case nameof(LineItem.RecipientAddress): // Changing the target pool can change fees.
			case nameof(LineItem.Amount):
				this.RecalculateAggregates();
				this.UpdateAreLineItemsValidProperty();
				break;
			case nameof(LineItem.IsLineItemValid):
				this.UpdateAreLineItemsValidProperty();
				break;
		}
	}

	private void UpdateAreLineItemsValidProperty()
	{
		this.AreLineItemsValid = this.LineItems.Count > 0 && this.LineItems.All(li => li.IsLineItemValid);
	}

	private void RecalculateAggregates()
	{
		Security security = this.SelectedAccount?.ZcashAccount.Network.AsSecurity() ?? UnknownSecurity;
		this.Subtotal = security.Amount(this.LineItems.Sum(li => li.Amount) ?? 0m);

		try
		{
			bool insufficientFunds;
			try
			{
				ImmutableArray<Transaction.LineItem> nonEmptyLineItems = this.GetLineItems();
				LightWalletClient.SpendDetails? details = nonEmptyLineItems.IsEmpty
					? default
					: this.SelectedAccount?.LightWalletClient?.SimulateSend(this.SelectedAccount.ZcashAccount, nonEmptyLineItems);
				insufficientFunds = false;
				if (details is not null)
				{
					this.Fee = this.SelectedSecurity.Amount(details.Fee);
					this.Total = this.Subtotal + this.Fee;
				}
				else
				{
					this.Fee = null;
					this.Total = null;
				}
			}
			catch (InsufficientFundsException ex)
			{
				insufficientFunds = true;
				this.Total = security.Amount(ex.RequiredBalance);
				this.Fee = this.Total - this.Subtotal;
			}

			if (insufficientFunds && this.SelectedAccount is not null)
			{
				// In preparing these error messages, we specifically do *not* disclose the user's actual balance
				// because the user's payee may be looking at the screen as well, and the payee shouldn't be privy
				// to the sender's balance.
				if (this.Total?.Amount > this.SelectedAccount.Balance.MainBalance.Amount + this.SelectedAccount.Balance.Incoming.Amount)
				{
					this.ErrorMessage = SendingStrings.InsufficientFunds;
				}
				else
				{
					this.ErrorMessage = SendingStrings.InsufficientSpendableFunds;
				}
			}
			else
			{
				this.ErrorMessage = null;
			}
		}
		catch (LightWalletException ex)
		{
			this.ErrorMessage = ex.Message;
		}
	}

	private LineItem AddLineItem()
	{
		LineItem lineItem = new(this);
		lineItem.PropertyChanged += this.LineItem_PropertyChanged;

		this.lineItems.Add(lineItem);
		this.UpdateAreLineItemsValidProperty();
		return lineItem;
	}

	private void Remove(LineItem lineItem)
	{
		this.lineItems.Remove(lineItem);
		this.RecalculateAggregates();
		this.UpdateAreLineItemsValidProperty();
	}

	public class LineItem : ViewModelBaseWithAccountSelector
	{
		private readonly SendingViewModel owner;
		private readonly ObservableAsPropertyHelper<ZcashAddress?> recipientAddressParsed;
		private readonly ObservableAsPropertyHelper<bool> isLineItemValid;
		private readonly ObservableAsPropertyHelper<bool> isMemoVisible;
		private string memo = string.Empty;
		private string recipientAddress = string.Empty;
		private bool isRecipientLocked;
		private string? recipientLabel;
		private string? message;
		private object? selectedRecipient;

		public LineItem(SendingViewModel owner)
			: base(owner.ViewModelServices, showOnlyAccountsWithSpendKeys: true)
		{
			this.owner = owner;

			this.AmountEntry = new DualAmountEntryViewModel(owner.ViewModelServices);
			this.AmountEntry.WhenPropertyChanged(e => e.Amount).Subscribe(_ => this.RaisePropertyChanged(nameof(this.Amount)));

			this.ScanCommand = ReactiveCommand.CreateFromTask(this.ScanAsync);
			this.RemoveLineItemCommand = ReactiveCommand.Create(() => this.owner.Remove(this));

			this.recipientAddressParsed = this.WhenAnyValue(
				vm => vm.SelectedRecipient,
				vm => vm.RecipientAddress,
				(selected, addr) => selected switch
				{
					Contact contact => contact.GetReceivingAddress(this.owner.SelectedAccount!.Network) ?? throw new InvalidOperationException("Missing address for contact"),
					Account account => account.ZcashAccount.DefaultAddress,
					null => ZcashAddress.TryDecode(addr, out _, out _, out ZcashAddress? parsed) ? parsed : null,
					_ => throw new InvalidOperationException("Unexpected recipient type"),
				}).ToProperty(this, nameof(this.RecipientAddressParsed));

			this.isLineItemValid = this.WhenAnyValue(
				vm => vm.Amount,
				vm => vm.RecipientAddressParsed,
				(amount, addr) => amount.HasValue && addr is not null)
				.ToProperty(this, nameof(this.IsLineItemValid));

			// Arrange tho hide the memo field if the address is valid and has no shielded receiver.
			// We want to show the memo field if the address is blank or invalid while the user sorts that out.
			this.isMemoVisible = this.WhenAnyValue(
				vm => vm.RecipientAddressParsed)
				.Select(addr => addr?.HasShieldedReceiver is not false)
				.ToProperty(this, nameof(this.IsMemoVisible));
		}

		public bool IsLineItemValid => this.isLineItemValid.Value;

		public string RecipientAddressCaption => SendingStrings.LineItem_RecipientAddressCaption;

		public string RecipientBoxWatermark => SendingStrings.LineItem_RecipientBoxWatermark;

		public bool IsRecipientLocked
		{
			get => this.isRecipientLocked;
			set => this.RaiseAndSetIfChanged(ref this.isRecipientLocked, value);
		}

		[Required, ZcashAddress]
		public string RecipientAddress
		{
			get => this.recipientAddress;
			set => this.RaiseAndSetIfChanged(ref this.recipientAddress, value);
		}

		public object? SelectedRecipient
		{
			get => this.selectedRecipient;
			set => this.RaiseAndSetIfChanged(ref this.selectedRecipient, value);
		}

		public ReadOnlyObservableCollection<object> PossibleRecipients => this.owner.possibleRecipients ?? throw Assumes.NotReachable();

		public ZcashAddress? RecipientAddressParsed => this.recipientAddressParsed.Value;

		public string? RecipientLabel
		{
			get => this.recipientLabel;
			set => this.RaiseAndSetIfChanged(ref this.recipientLabel, value);
		}

		public string AmountCaption => SendingStrings.LineItem_AmountCaption;

		public decimal? Amount
		{
			get => this.AmountEntry.Amount;
			set => this.AmountEntry.Amount = value;
		}

		public DualAmountEntryViewModel AmountEntry { get; }

		public bool IsMemoVisible => this.isMemoVisible.Value;

		public string MemoCaption => SendingStrings.LineItem_MemoCaption;

		public string Memo
		{
			get => this.memo;
			set => this.RaiseAndSetIfChanged(ref this.memo, value);
		}

		public string? Message
		{
			get => this.message;
			set => this.RaiseAndSetIfChanged(ref this.message, value);
		}

		public ReactiveCommand<Unit, Unit> ScanCommand { get; }

		public bool IsScanCommandAvailable => this.ViewModelServices.IsScanCommandAvailable;

		public string RemoveLineItemCommandCaption => SendingStrings.LineItem_RemoveLineItemCommandCaption;

		public ReactiveCommand<Unit, Unit> RemoveLineItemCommand { get; }

		private async Task ScanAsync()
		{
			try
			{
				MobileBarcodeScanner scanner = new();
				MobileBarcodeScanningOptions options = new()
				{
				};
				ZXing.Result result = await scanner.Scan(options);
			}
			catch (NotSupportedException)
			{
				// fallback to file picker
				// TODO
			}
		}
	}
}
