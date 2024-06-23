// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Reactive.Linq;
using DynamicData.Binding;
using Nerdbank.Cryptocurrencies;
using Nerdbank.Cryptocurrencies.Exchanges;

namespace Nerdbank.Zcash.App.ViewModels;

[DebuggerDisplay($"{{{nameof(DebuggerDisplay)},nq}}")]
public class TransactionViewModel : ViewModelBase, IViewModel<ZcashTransaction>
{
	private readonly HistoryViewModel owner;
	private readonly ObservableAsPropertyHelper<bool> isToAddressVisible;
	private readonly ObservableAsPropertyHelper<ReadOnlyCollection<LineItem>> lineItems;
	private readonly ObservableAsPropertyHelper<string?> memo;
	private readonly ObservableAsPropertyHelper<bool> isMutableMemoVisible;
	private readonly ObservableAsPropertyHelper<bool> isSubtotalVisible;
	private readonly ObservableAsPropertyHelper<bool> isSingleLineItem;
	private readonly ObservableAsPropertyHelper<bool> isMultiLineItem;
	private readonly ObservableAsPropertyHelper<string?> toAddress;
	private readonly ObservableAsPropertyHelper<SecurityAmount> subtotal;
	private readonly ObservableAsPropertyHelper<bool> canOtherPartyBeChanged;
	private SecurityAmount runningBalance;
	private SecurityAmount? alternateAmount;
	private string? alternateNetChangeUserEditInProgress;

	public TransactionViewModel(Security security, Security? alternateSecurity, ZcashTransaction transaction, HistoryViewModel owner)
	{
		this.Security = security;
		this.AlternateSecurity = alternateSecurity;
		this.Model = transaction;
		this.owner = owner;

		IObservable<ReadOnlyCollection<LineItem>> lineItemsTransform = this.WhenAnyValue(
			vm => vm.Model.SendItems,
			vm => vm.Model.RecvItems,
			(s, r) => this.InitializeLineItems(s, r));
		this.lineItems = lineItemsTransform.ToProperty(this, nameof(this.LineItems));
		this.memo = lineItemsTransform.Select(c => c.Count == 1 ? c[0].Memo : null).ToProperty(this, nameof(this.Memo));
		this.isMutableMemoVisible = lineItemsTransform.Select(c => c.Count > 0).ToProperty(this, nameof(this.IsMutableMemoVisible));
		this.isSingleLineItem = lineItemsTransform.Select(c => c.Count == 1).ToProperty(this, nameof(this.IsSingleLineItem));
		this.isMultiLineItem = lineItemsTransform.Select(c => c.Count > 1).ToProperty(this, nameof(this.IsMultiLineItem));
		this.toAddress = lineItemsTransform.Select(c => c.FirstOrDefault()?.ToAddress).ToProperty(this, nameof(this.ToAddress));
		this.isSubtotalVisible = lineItemsTransform.Select(c => c.Count > 0).ToProperty(this, nameof(this.IsSubtotalVisible));
		this.subtotal = lineItemsTransform.Select(c => this.Security.Amount(c.Sum(li => li.Amount.Amount))).ToProperty(this, nameof(this.Subtotal));
		this.canOtherPartyBeChanged = lineItemsTransform.Select(c => c.Count == 1 && c[0].CanOtherPartyBeChanged).ToProperty(this, nameof(this.CanOtherPartyBeChanged));
		lineItemsTransform.ToProperty(this, nameof(this.OtherPartyName));

		this.isToAddressVisible = this.WhenAnyValue(vm => vm.OtherPartyName, vm => vm.IsSingleLineItem, vm => vm.Owner.ShowProtocolDetails)
			.Select(((string? OtherPartyName, bool IsSingleLineItem, bool ShowProtocolDetails) x) => x.IsSingleLineItem && (string.IsNullOrEmpty(x.OtherPartyName) || x.ShowProtocolDetails))
			.ToProperty(this, nameof(this.IsToAddressVisible));

		this.LinkProperty(transaction, nameof(transaction.BlockNumber), nameof(this.BlockNumber));
		this.LinkProperty(transaction, nameof(transaction.When), nameof(this.When));
		this.LinkProperty(transaction, nameof(transaction.MutableMemo), nameof(this.MutableMemo));
		this.LinkProperty(transaction, nameof(transaction.Fee), nameof(this.Fee));
		this.LinkProperty(transaction, nameof(transaction.NetChange), nameof(this.NetChange));
		this.WhenPropertyChanged(vm => vm.NetChange, notifyOnInitialValue: false).Subscribe(_ => this.Owner.OnTransactionNetChange(this));

		this.LinkProperty(nameof(this.When), nameof(this.WhenColumnFormatted));
		this.LinkProperty(nameof(this.When), nameof(this.WhenDetailedFormatted));

		this.LinkProperty(nameof(this.AlternateNetChange), nameof(this.AlternateNetChangeUserEdit));

		if (transaction.When is not null && alternateSecurity is not null && owner.ViewModelServices.ExchangeData.TryGetExchangeRate(transaction.When.Value, new TradingPair(security, alternateSecurity), out ExchangeRate? exchangeRate))
		{
			this.AlternateNetChange = this.NetChange * exchangeRate;
		}
	}

	public HistoryViewModel Owner => this.owner;

	public ZcashTransaction Model { get; }

	public uint? BlockNumber => this.Model.BlockNumber;

	public string BlockNumberFormatted => $"{this.BlockNumber:N0}";

	public string BlockNumberCaption => TransactionStrings.BlockNumberCaption;

	public string TransactionId => this.Model.TransactionId?.ToString() ?? string.Empty;

	public string TransactionIdCaption => TransactionStrings.TransactionIdCaption;

	public DateTimeOffset? When => this.Model.When?.ToLocalTime();

	public string WhenColumnFormatted
	{
		get
		{
			if (this.When is null)
			{
				return string.Empty;
			}

			// If this happened today, just display the time.
			if (this.When.Value.Date == DateTimeOffset.Now.Date)
			{
				return $"{this.When:h:mm tt}";
			}

			int daysAgo = (DateTimeOffset.UtcNow - this.When.Value).Days;
			if (daysAgo < 7)
			{
				return $"{this.When:ddd}";
			}

			if (daysAgo < 6 * 30 || this.When.Value.Year == DateTimeOffset.UtcNow.Year)
			{
				return $"{this.When:d MMM}";
			}

			return $"{this.When:d}";
		}
	}

	public string WhenDetailedFormatted => this.When?.ToString("g") ?? string.Empty;

	public string WhenCaption => TransactionStrings.WhenCaption;

	/// <summary>
	/// Gets the transaction's full impact on the account balance (including fees).
	/// </summary>
	public SecurityAmount NetChange => this.Security.Amount(this.Model.NetChange);

	public bool IsSubtotalVisible => this.isSubtotalVisible.Value;

	/// <summary>
	/// Gets the sum of all line items in this transaction (the transaction's value, excluding fees).
	/// </summary>
	public SecurityAmount Subtotal => this.subtotal.Value;

	public string SubtotalCaption => this.IsIncoming ? TransactionStrings.SubtotalCaption_Received : TransactionStrings.SubtotalCaption_Sent;

	public SecurityAmount? Fee => this.Model.Fee is decimal fee ? this.Security.Amount(-fee) : null;

	public string FeeCaption => TransactionStrings.FeeCaption;

	public SecurityAmount? AlternateNetChange
	{
		get => this.alternateAmount;
		internal set
		{
			this.RaiseAndSetIfChanged(ref this.alternateAmount, value);
			this.alternateNetChangeUserEditInProgress = null;
			this.RecordValidationError(null, nameof(this.AlternateNetChangeUserEdit));
		}
	}

	public string AlternateNetChangeUserEdit
	{
		get
		{
			if (this.alternateNetChangeUserEditInProgress is null)
			{
				this.alternateNetChangeUserEditInProgress = this.AlternateNetChange?.Amount.ToString("F" + this.AlternateNetChange.Value.Security.Precision) ?? string.Empty;
				this.RecordValidationError(null, nameof(this.AlternateNetChangeUserEdit));
			}

			return this.alternateNetChangeUserEditInProgress;
		}

		set
		{
			Verify.Operation(this.AlternateSecurity is not null, "Cannot set alternate amount without an alternate security set.");
			this.alternateNetChangeUserEditInProgress = value;
			if (decimal.TryParse(value, CultureInfo.CurrentCulture, out decimal parsed))
			{
				this.RecordValidationError(null, nameof(this.AlternateNetChangeUserEdit));
				this.RaiseAndSetIfChanged(ref this.alternateAmount, this.AlternateSecurity.Amount(parsed), nameof(this.AlternateNetChange));

				// Record the exchange rate.
				if (this.When is not null)
				{
					ExchangeRate rate = new(this.AlternateSecurity.Amount(parsed), this.NetChange);
					this.owner.ViewModelServices.ExchangeData.SetExchangeRate(this.When.Value, rate);
				}
			}
			else
			{
				this.RecordValidationError(TransactionStrings.InvalidValue, nameof(this.AlternateNetChangeUserEdit));
			}

			this.RaisePropertyChanged();
		}
	}

	public string OtherPartyNameColumnHeader => TransactionStrings.OtherPartyNameColumnHeader;

	public string? OtherPartyName
	{
		get => this.LineItems?.Count switch
		{
			null => null,
			0 => null,
			1 => this.LineItems[0].OtherPartyName,
			_ => "(split)",
		};
		set
		{
			if (this.OtherPartyName != value)
			{
				Verify.Operation(this.CanOtherPartyBeChanged, "Other party cannot be changed.");
				switch (this.LineItems.Count)
				{
					case 1:
						// The property setter we're calling will raise a property changed notification for our own property.
						this.LineItems[0].OtherPartyName = value;
						break;
					default:
						throw new InvalidOperationException();
				}
			}
		}
	}

	public string OtherPartyNameCaption => TransactionStrings.OtherPartyNameCaption;

	public object? OtherParty
	{
		get => this.LineItems?.Count switch
		{
			null => null,
			0 => null,
			1 => this.LineItems[0].OtherParty,
			_ => "(split)",
		};
		set
		{
			if (this.OtherParty != value)
			{
				Verify.Operation(this.CanOtherPartyBeChanged, "Other party cannot be changed.");
				switch (this.LineItems.Count)
				{
					case 1:
						// The property setter we're calling will raise a property changed notification for our own property.
						this.LineItems[0].OtherParty = value;
						break;
					default:
						throw new InvalidOperationException();
				}
			}
		}
	}

	/// <inheritdoc cref="LineItem.CanOtherPartyBeChanged"/>
	public bool CanOtherPartyBeChanged => this.canOtherPartyBeChanged.Value;

	public ReadOnlyObservableCollection<Contact> OtherParties => this.owner.ViewModelServices.ContactManager.Contacts;

	public string? Memo => this.memo.Value;

	public bool IsSingleLineItem => this.isSingleLineItem.Value;

	public bool IsMultiLineItem => this.isMultiLineItem.Value;

	public string MemoCaption => TransactionStrings.MemoCaption;

	public string MutableMemoCaption => TransactionStrings.MutableMemoCaption;

	public bool IsToAddressVisible => this.isToAddressVisible.Value;

	public string? ToAddress => this.toAddress.Value;

	public string ToAddressCaption => TransactionStrings.ToAddressCaption;

	public bool IsMutableMemoVisible => this.isMutableMemoVisible.Value;

	public string MutableMemo
	{
		get => this.Model.MutableMemo;
		set => this.Model.MutableMemo = value;
	}

	public string LineItemsCaption => TransactionStrings.LineItemsCaption;

	public ReadOnlyCollection<LineItem> LineItems => this.lineItems.Value;

	public string AmountColumnHeader => TransactionStrings.AmountColumnHeader;

	public bool IsIncoming => this.Model.IsIncoming;

	public SecurityAmount RunningBalance
	{
		get => this.runningBalance;
		internal set => this.RaiseAndSetIfChanged(ref this.runningBalance, value);
	}

	private Security Security { get; }

	private Security? AlternateSecurity { get; }

	private string DebuggerDisplay => $"{this.WhenColumnFormatted} {this.NetChange}";

	private ReadOnlyCollection<LineItem> InitializeLineItems(ImmutableArray<ZcashTransaction.LineItem> sendItems, ImmutableArray<ZcashTransaction.LineItem> recvItems)
	{
		List<LineItem> lineItems = new(sendItems.Length + recvItems.Length);
		if (sendItems.Length > 0)
		{
			AddLineItems(sendItems, negate: true);
		}

		if (recvItems.Length > 0)
		{
			AddLineItems(recvItems);
		}

		void AddLineItems(ImmutableArray<ZcashTransaction.LineItem> items, bool negate = false)
		{
			if (items.Length == 2)
			{
				// Look for a common pattern of splitting a payment across two pools.
				// If that's what this is, just report the two line items as one.
				ZcashTransaction.LineItem first = items[0];
				ZcashTransaction.LineItem second = items[1];
				if (first.Memo.Equals(second.Memo) && first.Pool != second.Pool && first.Pool.HasValue && second.Pool.HasValue)
				{
					lineItems.Add(new LineItem(this, negate, first, second));
					return;
				}
			}

			lineItems.AddRange(items.Select(i => new LineItem(this, negate, i)));
		}

		return new(lineItems);
	}

	public class LineItem : ReactiveObject
	{
		private readonly TransactionViewModel owner;
		private readonly ZcashTransaction.LineItem model;
		private readonly ZcashTransaction.LineItem? additionalModel;
		private object? otherParty;
		private bool otherPartyLazyInitDone;

		public LineItem(TransactionViewModel owner, bool negate, ZcashTransaction.LineItem model, ZcashTransaction.LineItem? additionalModel = null)
		{
			this.owner = owner;
			this.model = model;
			this.additionalModel = additionalModel;

			this.model.WhenAnyValue(x => x.OtherPartyName).Subscribe(_ =>
			{
				this.RaisePropertyChanged(nameof(this.OtherPartyName));
				this.owner.RaisePropertyChanged(nameof(this.owner.OtherPartyName));
			});

			this.Amount = owner.Security.Amount(model.Amount + (additionalModel?.Amount ?? 0));
			this.otherParty = model.OtherParty;

			if (negate)
			{
				this.Amount = -this.Amount;
			}
		}

		public SecurityAmount Amount { get; }

		public string? Memo => this.model.Memo.Message;

		public string ToAddress => this.model.ToAddress;

		public object? OtherParty
		{
			get
			{
				this.LazyInitializeOtherParty();
				return this.otherParty;
			}

			set
			{
				if (this.otherParty != value)
				{
					this.otherParty = value;

					// The model only sees this if it's a Contact. Accounts are only ever set on the view model.
					this.model.OtherParty = (value as Contact)?.Id;
					if (this.additionalModel is not null)
					{
						this.additionalModel.OtherParty = (value as Contact)?.Id;
					}

					this.RaisePropertyChanged();
					this.owner.RaisePropertyChanged(nameof(this.owner.OtherParty));
				}
			}
		}

		/// <summary>
		/// Gets a value indicating whether <see cref="OtherParty"/> or <see cref="OtherPartyName"/> can be changed
		/// by the user.
		/// </summary>
		/// <value>
		/// Typically <see langword="true" />, but <see langword="false" /> when the other party is known to be an account
		/// in this app or when the transaction has more than one line item in it.
		/// </value>
		public bool CanOtherPartyBeChanged => this.OtherParty is not Account;

		public string? OtherPartyName
		{
			get
			{
				this.LazyInitializeOtherParty();
				return this.model.OtherPartyName;
			}

			set
			{
				if (this.model.OtherPartyName != value)
				{
					// Updating the model will trigger a PropertyChanged event for ourselves.
					this.model.OtherPartyName = value;
					if (this.additionalModel is not null)
					{
						this.additionalModel.OtherPartyName = value;
					}
				}
			}
		}

		public ReadOnlyObservableCollection<Contact> OtherParties => this.owner.OtherParties;

		private void LazyInitializeOtherParty()
		{
			if (this.otherParty is null && !this.otherPartyLazyInitDone)
			{
				Account? otherAccount = null;
				if (this.model.OtherParty is int otherPartyId && this.owner.owner.ViewModelServices.ContactManager.TryGetContact(otherPartyId, out Contact? otherParty))
				{
					this.otherParty = otherParty;
				}
				else if (this.owner.IsIncoming && this.owner.Model.TransactionId.HasValue)
				{
					// Incoming transactions are harder to track as to whether they came from another account,
					// so we search for the txid in other accounts to see if they sent it.
					otherAccount = this.owner.owner.ViewModelServices.Wallet.GetAccountsContainingTransaction(this.owner.Model.TransactionId.Value).Where(a => a != this.owner.owner.SelectedAccount).FirstOrDefault();
				}
				else
				{
					// Search to see if the other party is an account in this wallet.
					if (this.owner.owner.ViewModelServices.Wallet.TryGetAccountThatReceives(this.model.ToAddress, out Account? toAccount))
					{
						// We're sending to another account in the wallet.
						otherAccount = toAccount;
					}
				}

				// Update the other party name to whatever the current name of the account is.
				if (otherAccount is not null)
				{
					this.otherParty = otherAccount;
					this.model.OtherPartyName = otherAccount.Name;
				}
				else
				{
					this.model.TryAssignContactAsOtherParty(this.owner.owner.ViewModelServices.ContactManager);
				}

				// Remember that we searched (and failed) so we don't search again.
				this.otherPartyLazyInitDone = true;
			}
		}
	}
}
