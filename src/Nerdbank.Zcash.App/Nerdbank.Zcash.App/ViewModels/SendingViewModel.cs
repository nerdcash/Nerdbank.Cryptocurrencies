// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using DynamicData;
using DynamicData.Binding;
using Nerdbank.Cryptocurrencies.Exchanges;
using ZXing.Mobile;

namespace Nerdbank.Zcash.App.ViewModels;

public class SendingViewModel : ViewModelBaseWithAccountSelector, IHasTitle
{
	private readonly ObservableCollection<LineItem> lineItems = new();
	private readonly ReadOnlyObservableCollection<LineItem> lineItemsReadOnly;
	private SecurityAmount subtotal;
	private SecurityAmount total;
	private ReadOnlyObservableCollection<Contact>? possibleRecipients;
	private SecurityAmount fee;
	private IDisposable? possibleRecipientsSubscription;

	[Obsolete("For design-time use only.", error: true)]
	public SendingViewModel()
		: this(new DesignTimeViewModelServices())
	{
		this.fee = new(0.0001m, this.SelectedAccount?.Network.AsSecurity() ?? UnknownSecurity);
	}

	public SendingViewModel(IViewModelServices viewModelServices)
		: base(viewModelServices, showOnlyAccountsWithSpendKeys: true)
	{
		this.lineItemsReadOnly = new(this.lineItems);
		this.AddLineItem();

		this.WhenPropertyChanged(vm => vm.SelectedAccount, notifyOnInitialValue: true).Subscribe(_ => this.OnSelectedAccountChanged());

		this.SendCommand = ReactiveCommand.CreateFromTask(this.SendAsync);
		this.AddLineItemCommand = ReactiveCommand.Create(this.AddLineItem);
	}

	public string Title => "Send Zcash";

	public string FromAccountCaption => "From account:";

	public ReadOnlyObservableCollection<LineItem> LineItems => this.lineItemsReadOnly;

	public string AddLineItemCommandCaption => "➕ Add line item";

	public ReactiveCommand<Unit, LineItem> AddLineItemCommand { get; }

	public string FeeCaption => "Fee";

	public SecurityAmount Fee
	{
		get => this.fee;
		private set => this.RaiseAndSetIfChanged(ref this.fee, value);
	}

	public string SubtotalCaption => "Subtotal";

	public SecurityAmount Subtotal
	{
		get => this.subtotal;
		private set => this.RaiseAndSetIfChanged(ref this.subtotal, value);
	}

	public string TotalCaption => "Total";

	public SecurityAmount Total
	{
		get => this.total;
		private set => this.RaiseAndSetIfChanged(ref this.total, value);
	}

	public string SendCommandCaption => "📤 Send";

	public ReactiveCommand<Unit, Unit> SendCommand { get; }

	private void RefreshRecipientsList()
	{
		this.possibleRecipientsSubscription?.Dispose();

		// Prepare the allowed recipients list from the address book, filtered to those with
		// receiving addresses on the same network.
		this.possibleRecipientsSubscription = this.ViewModelServices.ContactManager.Contacts.AsObservableChangeSet()
			.AutoRefresh(c => c.ReceivingAddress)
			.Filter(c => c.ReceivingAddress is { } addr && addr.Network == this.SelectedAccount?.Network)
			.Bind(out this.possibleRecipients)
			.Subscribe();
		this.RaisePropertyChangedOnLineItems(nameof(LineItem.PossibleRecipients));
	}

	private void Remove(LineItem lineItem)
	{
		this.lineItems.Remove(lineItem);
	}

	private Task SendAsync()
	{
		// Block sending if validation errors exist.
		return Task.CompletedTask;
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
		LineItem changedLineItem = (LineItem)sender!;
		switch (e.PropertyName)
		{
			case nameof(LineItem.RecipientAddress): // Changing the target pool can change fees.
			case nameof(LineItem.Amount):
				this.RecalculateAggregates();
				break;
		}
	}

	private void RecalculateAggregates()
	{
		Security security = this.SelectedAccount?.ZcashAccount.Network.AsSecurity() ?? UnknownSecurity;

		this.Subtotal = new SecurityAmount(this.LineItems.Sum(li => li.Amount), security);

		this.Fee = new SecurityAmount(0.0001m, security); // TODO: calculate fee

		this.Total = this.Subtotal + this.Fee;
	}

	private void OnSelectedAccountChanged()
	{
		this.RefreshRecipientsList();
		this.RecalculateAggregates();

		foreach (LineItem lineItem in this.lineItems)
		{
			lineItem.SelectedAccount = this.SelectedAccount;
		}
	}

	private LineItem AddLineItem()
	{
		LineItem lineItem = new(this);
		lineItem.PropertyChanged += this.LineItem_PropertyChanged;

		this.lineItems.Add(lineItem);
		return lineItem;
	}

	public class LineItem : ViewModelBaseWithAccountSelector
	{
		private readonly SendingViewModel owner;
		private readonly ObservableAsPropertyHelper<string> tickerSymbol;
		private string memo = string.Empty;
		private string recipientAddress = string.Empty;
		private decimal amount;
		private Contact? selectedRecipient;

		public LineItem(SendingViewModel owner)
			: base(owner.ViewModelServices, showOnlyAccountsWithSpendKeys: true)
		{
			this.owner = owner;

			this.tickerSymbol = this.WhenAnyValue(
				vm => vm.SelectedAccount,
				a => a?.Network.GetTickerName() ?? UnknownSecurity.TickerSymbol).ToProperty(this, nameof(this.TickerSymbol));

			this.ScanCommand = ReactiveCommand.CreateFromTask(this.ScanAsync);
			this.RemoveLineItemCommand = ReactiveCommand.Create(() => this.owner.Remove(this));
		}

		public string RecipientAddressCaption => "Recipient:";

		public string RecipientBoxWatermark => "Zcash address or contact name";

		[Required, ZcashAddress]
		public string RecipientAddress
		{
			get => this.recipientAddress;
			set => this.RaiseAndSetIfChanged(ref this.recipientAddress, value);
		}

		public Contact? SelectedRecipient
		{
			get => this.selectedRecipient;
			set => this.RaiseAndSetIfChanged(ref this.selectedRecipient, value);
		}

		public ReadOnlyObservableCollection<Contact> PossibleRecipients => this.owner.possibleRecipients ?? throw Assumes.NotReachable();

		public string AmountCaption => "Amount:";

		public decimal Amount
		{
			get => this.amount;
			set => this.RaiseAndSetIfChanged(ref this.amount, value);
		}

		public string TickerSymbol => this.tickerSymbol.Value;

		public string MemoCaption => "Memo:";

		public string Memo
		{
			get => this.memo;
			set => this.RaiseAndSetIfChanged(ref this.memo, value);
		}

		public ReactiveCommand<Unit, Unit> ScanCommand { get; }

		public string RemoveLineItemCommandCaption => "❌";

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
