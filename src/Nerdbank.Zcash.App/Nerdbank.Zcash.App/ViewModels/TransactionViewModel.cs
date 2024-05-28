// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Reactive.Linq;
using Nerdbank.Cryptocurrencies;
using Nerdbank.Cryptocurrencies.Exchanges;

namespace Nerdbank.Zcash.App.ViewModels;

public class TransactionViewModel : ViewModelBase, IViewModel<ZcashTransaction>
{
	private readonly HistoryViewModel owner;
	private readonly ObservableAsPropertyHelper<bool> isToAddressVisible;
	private SecurityAmount runningBalance;
	private SecurityAmount? alternateAmount;

	public TransactionViewModel(Security security, Security? alternateSecurity, ZcashTransaction transaction, HistoryViewModel owner)
	{
		this.Security = security;
		this.AlternateSecurity = alternateSecurity;
		this.Model = transaction;
		this.owner = owner;

		this.LineItems = this.InitializeLineItems();

		if (this.LineItems.Count == 1)
		{
			this.Memo = this.LineItems[0].Memo;
		}

		this.isToAddressVisible = this.WhenAnyValue(vm => vm.OtherPartyName, vm => vm.IsSingleLineItem, vm => vm.Owner.ShowProtocolDetails)
			.Select(((string? OtherPartyName, bool IsSingleLineItem, bool ShowProtocolDetails) x) => x.IsSingleLineItem && (string.IsNullOrEmpty(x.OtherPartyName) || x.ShowProtocolDetails))
			.ToProperty(this, nameof(this.IsToAddressVisible));

		this.LinkProperty(transaction, nameof(transaction.BlockNumber), nameof(this.BlockNumber));
		this.LinkProperty(transaction, nameof(transaction.When), nameof(this.When));
		this.LinkProperty(transaction, nameof(transaction.MutableMemo), nameof(this.MutableMemo));

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

	public bool IsSubtotalVisible => this.LineItems.Count > 0;

	/// <summary>
	/// Gets the sum of all line items in this transaction (the transaction's value, excluding fees).
	/// </summary>
	public SecurityAmount Subtotal => this.Security.Amount(this.LineItems.Sum(li => li.Amount.Amount));

	public string SubtotalCaption => this.IsIncoming ? TransactionStrings.SubtotalCaption_Received : TransactionStrings.SubtotalCaption_Sent;

	public SecurityAmount? Fee => this.Model.Fee is decimal fee ? this.Security.Amount(-fee) : null;

	public string FeeCaption => TransactionStrings.FeeCaption;

	public SecurityAmount? AlternateNetChange
	{
		get => this.alternateAmount;
		internal set => this.RaiseAndSetIfChanged(ref this.alternateAmount, value);
	}

	public decimal? AlternateNetChangeUserEdit
	{
		get => this.AlternateNetChange?.Amount;
		set
		{
			Verify.Operation(this.AlternateSecurity is not null, "Cannot set alternate amount without an alternate security set.");
			this.AlternateNetChange = value is null ? null : this.Security.Amount(value.Value);

			// Record the exchange rate.
			if (value is not null && this.When is not null)
			{
				ExchangeRate rate = new(this.AlternateSecurity.Amount(value.Value), this.NetChange);
				this.owner.ViewModelServices.ExchangeData.SetExchangeRate(this.When.Value, rate);
			}

			this.RaisePropertyChanged();
		}
	}

	public string OtherPartyNameColumnHeader => TransactionStrings.OtherPartyNameColumnHeader;

	public string? OtherPartyName
	{
		get => this.LineItems.Count switch
		{
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
		get => this.LineItems.Count switch
		{
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
	public bool CanOtherPartyBeChanged => this.LineItems.Count == 1 && this.LineItems[0].CanOtherPartyBeChanged;

	public ReadOnlyObservableCollection<Contact> OtherParties => this.owner.ViewModelServices.ContactManager.Contacts;

	public string? Memo { get; }

	public bool IsSingleLineItem => this.LineItems.Count == 1;

	public bool IsMultiLineItem => this.LineItems.Count > 1;

	public string MemoCaption => TransactionStrings.MemoCaption;

	public string MutableMemoCaption => TransactionStrings.MutableMemoCaption;

	public bool IsToAddressVisible => this.isToAddressVisible.Value;

	public string? ToAddress => this.LineItems.FirstOrDefault()?.ToAddress;

	public string ToAddressCaption => TransactionStrings.ToAddressCaption;

	public bool IsMutableMemoVisible => this.LineItems.Count > 0;

	public string MutableMemo
	{
		get => this.Model.MutableMemo;
		set => this.Model.MutableMemo = value;
	}

	public string LineItemsCaption => TransactionStrings.LineItemsCaption;

	public ReadOnlyCollection<LineItem> LineItems { get; }

	public string AmountColumnHeader => TransactionStrings.AmountColumnHeader;

	public bool IsIncoming => this.Model.IsIncoming;

	public SecurityAmount RunningBalance
	{
		get => this.runningBalance;
		internal set => this.RaiseAndSetIfChanged(ref this.runningBalance, value);
	}

	private Security Security { get; }

	private Security? AlternateSecurity { get; }

	private ReadOnlyCollection<LineItem> InitializeLineItems()
	{
		List<LineItem> lineItems = new(this.Model.SendItems.Length + this.Model.RecvItems.Length);
		if (this.Model.SendItems.Length > 0)
		{
			AddLineItems(this.Model.SendItems.ToImmutableArray(), negate: true);
		}

		if (this.Model.RecvItems.Length > 0)
		{
			AddLineItems(this.Model.RecvItems);
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
