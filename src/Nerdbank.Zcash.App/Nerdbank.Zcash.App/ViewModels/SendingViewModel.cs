// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Reactive.Linq;
using DynamicData;
using DynamicData.Binding;
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
	private SecurityAmount subtotal;
	private SecurityAmount total;
	private ReadOnlyObservableCollection<object>? possibleRecipients;
	private SecurityAmount fee;
	private IDisposable? possibleRecipientsSubscription;

	[Obsolete("For design-time use only.", error: true)]
	public SendingViewModel()
		: this(new DesignTimeViewModelServices())
	{
		this.LineItems[0].AmountEntry.Amount = 1.23m;
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

		this.SendCommand = ReactiveCommand.CreateFromTask(this.SendAsync);
		this.AddLineItemCommand = ReactiveCommand.Create(this.AddLineItem);
	}

	public string Title => "Send Zcash";

	public SyncProgressData SyncProgress { get; }

	public string FromAccountCaption => "From account:";

	public string TestNetWarning => Strings.TestNetIsWorthlessWarning;

	public bool IsTestNetWarningVisible => this.isTestNetWarningVisible.Value;

	public ReadOnlyObservableCollection<LineItem> LineItems => this.lineItemsReadOnly;

	public string AddLineItemCommandCaption => "➕ Add line item";

	public ReactiveCommand<Unit, LineItem> AddLineItemCommand { get; }

	public string FeeCaption => "Fee";

	public SecurityAmount Fee
	{
		get => this.fee;
		private set => this.RaiseAndSetIfChanged(ref this.fee, value);
	}

	public SecurityAmount? FeeAlternate => this.feeAlternate.Value;

	public string SubtotalCaption => "Subtotal";

	public SecurityAmount Subtotal
	{
		get => this.subtotal;
		private set => this.RaiseAndSetIfChanged(ref this.subtotal, value);
	}

	public SecurityAmount? SubtotalAlternate => this.subtotalAlternate.Value;

	public string TotalCaption => "Total";

	public SecurityAmount Total
	{
		get => this.total;
		private set => this.RaiseAndSetIfChanged(ref this.total, value);
	}

	public SecurityAmount? TotalAlternate => this.totalAlternate.Value;

	public string SendCommandCaption => "📤 Send";

	public ReactiveCommand<Unit, Unit> SendCommand { get; }

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
			.AutoRefresh(c => c.ReceivingAddress)
			.Filter(c => c.ReceivingAddress is { } addr && addr.Network == selectedAccount?.Network)
			.Transform(c => c as object);

		this.possibleRecipientsSubscription = accounts.Merge(contacts)
			.Bind(out this.possibleRecipients)
			.Subscribe();
		this.RaisePropertyChangedOnLineItems(nameof(LineItem.PossibleRecipients));
	}

	private void Remove(LineItem lineItem)
	{
		this.lineItems.Remove(lineItem);
	}

	private async Task SendAsync(CancellationToken cancellationToken)
	{
		// TODO: Block sending if validation errors exist.
		Verify.Operation(this.SelectedAccount?.LightWalletClient is not null, "No lightclient.");

		IEnumerable<LightWalletClient.TransactionSendItem> lineItemsPrep =
			from li in this.LineItems
			let to = li.SelectedRecipient switch
			{
				Contact contact => contact.ReceivingAddress ?? throw new InvalidOperationException("Missing address for contact"),
				Account account => account.ZcashAccount.DefaultAddress,
				null => ZcashAddress.Decode(li.RecipientAddress),
				_ => throw new InvalidOperationException($"Unknown type of selected recipient: {li.SelectedRecipient.GetType().Name}"),
			}
			select new LightWalletClient.TransactionSendItem(to, li.Amount ?? 0m, Memo.FromMessage(li.Memo));
		List<LightWalletClient.TransactionSendItem> lineItems = lineItemsPrep.ToList();

		Progress<LightWalletClient.SendProgress> progress = new(ProgressUpdate);
		await this.SelectedAccount.LightWalletClient.SendAsync(
			lineItems,
			new Progress<LightWalletClient.SendProgress>(ProgressUpdate),
			cancellationToken);

		void ProgressUpdate(LightWalletClient.SendProgress progress)
		{
			if (progress.Total > 0)
			{
				Debug.WriteLine($"Sending {progress.Progress}/{progress.Total} ({progress.Progress * 100 / progress.Total}%)");
			}
		}
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
				break;
		}
	}

	private void RecalculateAggregates()
	{
		Security security = this.SelectedAccount?.ZcashAccount.Network.AsSecurity() ?? UnknownSecurity;

		this.Subtotal = new SecurityAmount(this.LineItems.Sum(li => li.Amount) ?? 0m, security);

		this.Fee = new SecurityAmount(0.0001m, security); // TODO: calculate fee

		this.Total = this.Subtotal + this.Fee;
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
		private string memo = string.Empty;
		private string recipientAddress = string.Empty;
		private object? selectedRecipient;

		public LineItem(SendingViewModel owner)
			: base(owner.ViewModelServices, showOnlyAccountsWithSpendKeys: true)
		{
			this.owner = owner;

			this.AmountEntry = new DualAmountEntryViewModel(owner.ViewModelServices);
			this.AmountEntry.WhenPropertyChanged(e => e.Amount).Subscribe(_ => this.RaisePropertyChanged(nameof(this.Amount)));

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

		public object? SelectedRecipient
		{
			get => this.selectedRecipient;
			set => this.RaiseAndSetIfChanged(ref this.selectedRecipient, value);
		}

		public ReadOnlyObservableCollection<object> PossibleRecipients => this.owner.possibleRecipients ?? throw Assumes.NotReachable();

		public string AmountCaption => "Amount:";

		public decimal? Amount
		{
			get => this.AmountEntry.Amount;
			set => this.AmountEntry.Amount = value;
		}

		public DualAmountEntryViewModel AmountEntry { get; }

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
