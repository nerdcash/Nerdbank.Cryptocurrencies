// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.ComponentModel;
using MessagePack;
using Nerdbank.Cryptocurrencies.Exchanges;

namespace Nerdbank.Zcash.App.Models;

[MessagePackObject]
public class Account : ReactiveObject, IPersistableData
{
	private string name = string.Empty;
	private AccountBalances balance;
	private ulong? rebirthHeight;
	private ulong? optimizedBirthdayHeight;
	private bool isDirty = true;
	private LightWalletClient.SyncProgress? syncProgress;
	private uint lastBlockHeight;
	private ObservableCollection<ZcashTransaction> transactionsMutable = new();

	public Account(ZcashAccount account)
	{
		this.ZcashAccount = account;
		this.balance = new AccountBalances { };

		this.transactionsMutable = new ObservableCollection<ZcashTransaction>();
		this.Transactions = new ReadOnlyObservableCollection<ZcashTransaction>(this.transactionsMutable);

		this.MarkSelfDirtyOnPropertyChanged();
		this.MarkSelfDirtyOnCollectionChanged(this.TransactionsMutable);
	}

	[IgnoreMember]
	public bool IsDirty
	{
		get => this.isDirty;
		set => this.RaiseAndSetIfChanged(ref this.isDirty, value);
	}

	[Key(0)]
	public ZcashAccount ZcashAccount { get; }

	[IgnoreMember]
	public ZcashNetwork Network => this.ZcashAccount.Network;

	[Key(1)]
	public string Name
	{
		get => this.name;
		set => this.RaiseAndSetIfChanged(ref this.name, value);
	}

	/// <summary>
	/// Gets or sets the balances that are useful to convey to the user.
	/// </summary>
	[IgnoreMember]
	public AccountBalances Balance
	{
		get => this.balance;
		set => this.RaiseAndSetIfChanged(ref this.balance, value);
	}

	[Key(2)]
	public uint LastBlockHeight
	{
		get => this.lastBlockHeight;
		set => this.RaiseAndSetIfChanged(ref this.lastBlockHeight, value);
	}

	/// <summary>
	/// Gets or sets the "rebirth height" for this account.
	/// </summary>
	/// <value>A block height, or <see langword="null" /> if no transaction providing unspent inputs exists.</value>
	/// <remarks>
	/// The "rebirth height" is the block height that could be given as the birthday height when importing this account
	/// elsewhere, such that all spendable funds are available, although transaction history that contains
	/// already-spent funds may be missed.
	/// It is defined at the number of the block that contains the oldest unspent note or UTXO on the account.
	/// </remarks>
	[Key(4)]
	public ulong? RebirthHeight
	{
		get => this.rebirthHeight;
		set => this.RaiseAndSetIfChanged(ref this.rebirthHeight, value);
	}

	/// <summary>
	/// Gets or sets the number of the first block that this account has any transactions in.
	/// </summary>
	/// <value>A block height, or <see langword="null" /> if no transaction has been discovered on the block chain for this account.</value>
	/// <remarks>
	/// This value will always be at least the <see cref="ZcashAccount.BirthdayHeight"/>,
	/// since any transaction earlier than that block height would have been missed in the sync.
	/// </remarks>
	[Key(5)]
	public ulong? OptimizedBirthdayHeight
	{
		get => this.optimizedBirthdayHeight;
		set => this.RaiseAndSetIfChanged(ref this.optimizedBirthdayHeight, value);
	}

	[IgnoreMember]
	public ReadOnlyObservableCollection<ZcashTransaction> Transactions { get; private set; }

	[Key(3), EditorBrowsable(EditorBrowsableState.Never)]
	public ObservableCollection<ZcashTransaction> TransactionsMutable
	{
		get => this.transactionsMutable;
		set
		{
			if (this.transactionsMutable != value)
			{
				this.transactionsMutable = value;
				this.RaisePropertyChanged();
				this.Transactions = new ReadOnlyObservableCollection<ZcashTransaction>(value);
			}
		}
	}

	[IgnoreMember]
	public LightWalletClient? LightWalletClient { get; internal set; }

	[IgnoreMember]
	public LightWalletClient.SyncProgress? SyncProgress
	{
		get => this.syncProgress;
		set => this.RaiseAndSetIfChanged(ref this.syncProgress, value);
	}

	[IgnoreMember]
	public SendProgressData SendProgress { get; } = new();

	/// <summary>
	/// Adds a outgoing transaction to the list of transactions,
	/// even before it has been fully created and broadcast by the lightwallet client library.
	/// </summary>
	/// <param name="transaction">The provisional transaction.</param>
	public void AddProvisionalTransaction(ZcashTransaction transaction)
	{
		Requires.Argument(transaction.IsProvisionalTransaction, nameof(transaction), "Must be a provisional transaction.");
		this.TransactionsMutable.Add(transaction);
	}

	public void AddTransactions(IEnumerable<Transaction> transactions, uint? upToBlockNumber, ExchangeRateRecord exchangeRateRecord, AppSettings appSettings, ZcashWallet wallet, IContactManager contactManager)
	{
		uint highestBlockNumber = 0;
		foreach (Transaction transaction in transactions)
		{
			ZcashTransaction? tx = this.TransactionsMutable.FirstOrDefault(t => t.TransactionId == transaction.TransactionId);
			if (tx is null)
			{
				// Although no transaction had a matching txid, this may match a
				// provisional transaction, in which case, we should fill in the details.
				tx = this.TransactionsMutable.FirstOrDefault(t => t.IsProvisionalTransaction && t.SendItems.FirstOrDefault() is { } si1 &&
					transaction.Outgoing.Any(si2 => Equals_AllowApproximateRecipientMatch(si1, si2)));
			}

			if (tx is not null)
			{
				// We already have this transaction
				// Copy over elements that can change as a transaction gets confirmed or transitions from being provisional.
				tx.BlockNumber = transaction.MinedHeight;

				// If we're finalizing a provisional transaction, fill in extra details.
				// The transaction ID itself may have been filled in by the send view model,
				// even if some of the other details haven't been fleshed out by importing the transaction from the rust side.
				tx.TransactionId = transaction.TransactionId;

				// Take special care to migrate the exchange rate from the provisional transaction's timestamp to the confirmed one.
				if (tx.When != transaction.When && transaction.When.HasValue)
				{
					TradingPair pair = new(appSettings.AlternateCurrency, this.ZcashAccount.Network.AsSecurity());
					if (tx.When.HasValue && exchangeRateRecord.TryGetExchangeRate(tx.When.Value, pair, out ExchangeRate rate))
					{
						exchangeRateRecord.SetExchangeRate(transaction.When.Value, rate);
					}

					// Only set the When property *after* we've considered updating the exchange rate record.
					tx.When = transaction.When;
				}
			}
			else
			{
				tx = new ZcashTransaction
				{
					BlockNumber = transaction.MinedHeight,
					TransactionId = transaction.TransactionId,
					IsIncoming = transaction.IsIncoming,
					When = transaction.When,
					RecvItems = [.. from recv in transaction.Incoming
									where !recv.IsChange
									select new ZcashTransaction.LineItem(recv)],
					SendItems = transaction.Outgoing.Select(i => new ZcashTransaction.LineItem(i)).ToImmutableArray(),
					////Fee = transaction.IsIncoming ? null : -transaction.Fee, // ZingoLib is still buggy
				};

				this.TransactionsMutable.Add(tx);
			}

			this.AssignOtherParty(tx, wallet, contactManager);

			highestBlockNumber = Math.Max(highestBlockNumber, tx.BlockNumber ?? 0);
		}

		if (upToBlockNumber > this.LastBlockHeight)
		{
			this.LastBlockHeight = upToBlockNumber.Value;
		}

		// It's possible that our caller doesn't know the highest block number,
		// so we'll just use the highest one we saw if it exceeds what they told us.
		if (highestBlockNumber > this.LastBlockHeight)
		{
			this.LastBlockHeight = highestBlockNumber;
		}
	}

	public override string ToString() => this.Name;

	private static bool Equals_AllowApproximateRecipientMatch(ZcashTransaction.LineItem left, Transaction.SendItem right)
	{
		// We allow the ToAddress to match approximately because if it was originally given
		// as a compound unified address, the transaction we download will have only one receiver.
		return left.Amount == right.Amount
			&& left.Memo.Equals(right.Memo)
			&& left.ToAddress.IsMatch(right.ToAddress).HasFlag(ZcashAddress.Match.MatchingReceiversFound);
	}

	/// <summary>
	/// Assigns the <see cref="ZcashTransaction.LineItem.OtherParty"/> properties on a given transaction based on the addresses
	/// used in the transaction, if a match can be found.
	/// </summary>
	/// <param name="transaction">The transaction to adjust.</param>
	/// <param name="wallet">The collection of accounts that may be referenced.</param>
	/// <param name="contactManager">The contact manager.</param>
	private void AssignOtherParty(ZcashTransaction transaction, ZcashWallet wallet, IContactManager contactManager)
	{
		foreach (ZcashTransaction.LineItem sendItem in transaction.SendItems)
		{
			if (sendItem is { OtherParty: null, OtherPartyName: null })
			{
				if (contactManager.FindContact(sendItem.ToAddress, out Contact? contact) == ZcashAddress.Match.MatchingReceiversFound)
				{
					sendItem.OtherParty = contact;
				}
			}
		}

		foreach (ZcashTransaction.LineItem recvItem in transaction.RecvItems)
		{
			if (recvItem is { OtherParty: null, OtherPartyName: null })
			{
				// Detect the sender by the address they used to reach us.
				if (contactManager.TryGetContact(this, recvItem.ToAddress, out Contact? contact))
				{
					recvItem.OtherParty = contact;
				}
			}
		}
	}
}
