// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Diagnostics;
using MessagePack;

namespace Nerdbank.Zcash.App.Models;

[DebuggerDisplay($"{{{nameof(DebuggerDisplay)},nq}}")]
[MessagePackObject]
public class ZcashTransaction : ReactiveObject, IPersistableDataHelper
{
	internal static readonly TxId? ProvisionalTransactionId = null;
	private TxId? txid;
	private uint? blockNumber;
	private DateTimeOffset? when;
	private bool isDirty;
	private string mutableMemo = string.Empty;
	private decimal? fee;
	private ImmutableArray<LineItem> sendItems = ImmutableArray<LineItem>.Empty;
	private ImmutableArray<LineItem> recvItems = ImmutableArray<LineItem>.Empty;

	public ZcashTransaction()
	{
		this.MarkSelfDirtyOnPropertyChanged();
	}

	[Key(0)]
	public required TxId? TransactionId
	{
		get => this.txid;
		set
		{
			this.RaiseAndSetIfChanged(ref this.txid, value);
			this.RaisePropertyChanged(nameof(this.IsProvisionalTransaction));
		}
	}

	/// <summary>
	/// Gets a value indicating whether this transaction is provisional, i.e. it has not yet been mined into a block.
	/// </summary>
	[IgnoreMember]
	public bool IsProvisionalTransaction => this.TransactionId is null;

	[Key(1)]
	public uint? BlockNumber
	{
		get => this.blockNumber;
		set => this.RaiseAndSetIfChanged(ref this.blockNumber, value);
	}

	[Key(2)]
	public required bool IsIncoming { get; init; }

	[Key(3)]
	public DateTimeOffset? When
	{
		get => this.when;
		set => this.RaiseAndSetIfChanged(ref this.when, value);
	}

	[IgnoreMember]
	public decimal NetChange => this.RecvItems.Sum(i => i.Amount) - this.SendItems.Sum(i => i.Amount) - (this.Fee ?? 0);

	/// <summary>
	/// Gets the fee paid for this transaction, if known.
	/// </summary>
	/// <value>When specified, this is represented as a positive value.</value>
	[Key(4)]
	public decimal? Fee
	{
		get => this.fee;
		init
		{
			Requires.Range(value is not < 0, nameof(value), "Non-negative values only.");
			this.fee = value;
		}
	}

	[Key(5)]
	public string MutableMemo
	{
		get => this.mutableMemo;
		set => this.RaiseAndSetIfChanged(ref this.mutableMemo, value);
	}

	[Key(8)]
	public ImmutableArray<LineItem> SendItems
	{
		get => this.sendItems;
		init
		{
			this.RaiseAndSetIfChanged(ref this.sendItems, value);
			this.StartWatchingForDirtyChildren(value);
		}
	}

	[Key(9)]
	public ImmutableArray<LineItem> RecvItems
	{
		get => this.recvItems;
		init
		{
			this.RaiseAndSetIfChanged(ref this.recvItems, value);
			this.StartWatchingForDirtyChildren(value);
		}
	}

	[IgnoreMember]
	public bool IsDirty
	{
		get => this.isDirty;
		set => this.RaiseAndSetIfChanged(ref this.isDirty, value);
	}

	private string DebuggerDisplay => $"{this.TransactionId} ({this.NetChange})";

	/// <summary>
	/// Gets the incoming amount in this transaction that was received with one of the receivers in a particular address.
	/// </summary>
	/// <param name="address">The address of interest.</param>
	/// <returns>The sum of the amounts in the notes destined for the given address.</returns>
	public decimal GetAmountReceivedUsingAddress(ZcashAddress address)
	{
		IEnumerable<decimal> matchingNoteAmounts =
			from item in this.RecvItems
			where item.ToAddress.IsMatch(address).HasFlag(ZcashAddress.Match.MatchingReceiversFound)
			select item.Amount;
		return matchingNoteAmounts.Sum();
	}

	void IPersistableDataHelper.ClearDirtyFlagOnMembers()
	{
		this.SendItems.ClearDirtyFlag();
		this.RecvItems.ClearDirtyFlag();
	}

	void IPersistableDataHelper.OnPropertyChanged(string propertyName)
	{
		this.RaisePropertyChanged(propertyName);
	}

	/// <summary>
	/// Describes an individual spend in a transaction.
	/// </summary>
	[DebuggerDisplay($"{{{nameof(DebuggerDisplay)},nq}}")]
	[MessagePackObject]
	public class LineItem : ReactiveObject, IPersistableData
	{
		private bool isDirty;
		private int? otherParty;
		private string? otherPartyName;

		public LineItem()
		{
			this.MarkSelfDirtyOnPropertyChanged();
		}

		[SetsRequiredMembers]
		internal LineItem(Transaction.LineItem item)
			: this()
		{
			this.ToAddress = item.ToAddress;
			this.Amount = item.Amount;
			this.Memo = item.Memo;
		}

		/// <summary>
		/// Gets the recipient of this Zcash.
		/// </summary>
		[Key(0)]
		public required ZcashAddress ToAddress { get; init; }

		/// <summary>
		/// Gets magnitude of the impact this line item has on the account balance.
		/// </summary>
		/// <value>
		/// A positive value for incoming funds, or a negative value for outgoing funds.
		/// </value>
		[Key(1)]
		public required decimal Amount { get; init; }

		/// <summary>
		/// Gets the memo that is shared between sender and recipient.
		/// </summary>
		[Key(2)]
		public required Memo Memo { get; init; }

		/// <summary>
		/// Gets or sets the <see cref="Contact.Id"/> for the contact that sent or received this transaction.
		/// </summary>
		/// <remarks>
		/// <para>This will be <see langword="null" /> when the transaction was sent to another account controlled by this same user,
		/// or when we don't already know who the transaction was with and the user hasn't set the value yet.</para>
		/// </remarks>
		[Key(3)]
		public int? OtherParty
		{
			get => this.otherParty;
			set => this.RaiseAndSetIfChanged(ref this.otherParty, value);
		}

		/// <summary>
		/// Gets or sets the name of the person from whom funds were received.
		/// </summary>
		/// <value>This should generally be <see langword="null" /> when <see cref="OtherParty"/> is non-null,
		/// since <see cref="Contact.Name"/> should be displayed to the user when <see cref="OtherParty"/> is set.</value>
		/// <remarks>
		/// It's important to support the user in changing the name that appears on a transaction,
		/// even if the transaction was received using a diversified address that was assigned to some (other) contact,
		/// since it's a real world possibility that the other contact shared the address with others, such that
		/// the received funds were not in fact sent by the originally expected contact.
		/// </remarks>
		[Key(4)]
		public string? OtherPartyName
		{
			get => this.otherPartyName;
			set => this.RaiseAndSetIfChanged(ref this.otherPartyName, value);
		}

		[IgnoreMember]
		public bool IsDirty
		{
			get => this.isDirty;
			set => this.RaiseAndSetIfChanged(ref this.isDirty, value);
		}

		/// <summary>
		/// Gets the pool that received this note.
		/// </summary>
		[IgnoreMember]
		public Pool? Pool
		{
			get => this.ToAddress switch
			{
				TransparentAddress => Zcash.Pool.Transparent,
				SaplingAddress => Zcash.Pool.Sapling,
				OrchardAddress => Zcash.Pool.Orchard,
				UnifiedAddress => null, // compound address -- not expected, but zingolib is producing these.
				null => throw new InvalidOperationException("This struct hasn't been initialized."),
				_ => throw new NotSupportedException(),
			};
		}

		private string DebuggerDisplay => $"{this.Amount} {this.ToAddress} ({this.Memo})";

		internal bool TryAssignContactAsOtherParty(IContactManager contactManager)
		{
			if (this is { OtherParty: null, OtherPartyName: null })
			{
				if (contactManager.FindContact(this.ToAddress, out Contact? contact) == ZcashAddress.Match.MatchingReceiversFound && contact is not null)
				{
					this.OtherParty = contact.Id;
					this.OtherPartyName = contact.Name;
					return true;
				}
			}

			return false;
		}
	}
}
