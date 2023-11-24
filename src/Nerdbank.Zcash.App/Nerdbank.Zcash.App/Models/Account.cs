// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.ComponentModel;
using MessagePack;

namespace Nerdbank.Zcash.App.Models;

[MessagePackObject]
public class Account : ReactiveObject, IPersistableData
{
	private string? zingoWalletFileName;
	private string name = string.Empty;
	private AccountBalances balance;
	private bool isDirty;
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
	public string? WalletFileName
	{
		get => this.zingoWalletFileName;
		set => this.RaiseAndSetIfChanged(ref this.zingoWalletFileName, value);
	}

	[Key(3)]
	public uint LastBlockHeight
	{
		get => this.lastBlockHeight;
		set => this.RaiseAndSetIfChanged(ref this.lastBlockHeight, value);
	}

	[IgnoreMember]
	public ReadOnlyObservableCollection<ZcashTransaction> Transactions { get; private set; }

	[Key(4), EditorBrowsable(EditorBrowsableState.Never)]
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

	public void AddTransactions(IEnumerable<Transaction> transactions, uint? upToBlockNumber)
	{
		uint highestBlockNumber = 0;
		foreach (Transaction transaction in transactions)
		{
			ZcashTransaction? tx = this.TransactionsMutable.FirstOrDefault(t => t.TransactionId == transaction.TransactionId);
			if (tx is not null)
			{
				// We already have this transaction
				// Copy over elements that can change over time.
				tx.BlockNumber = transaction.IsUnconfirmed ? null : transaction.BlockNumber;
				tx.When = transaction.When;
			}
			else
			{
				tx = new ZcashTransaction
				{
					BlockNumber = transaction.IsUnconfirmed ? null : transaction.BlockNumber,
					TransactionId = transaction.TransactionId,
					IsIncoming = transaction.IsIncoming,
					When = transaction.When,
					RecvItems = transaction.Notes.Where(r => !r.IsChange).ToImmutableArray(),
					SendItems = transaction.Sends,
					Security = this.Network.AsSecurity(),
					Fee = transaction.IsIncoming ? null : -transaction.Fee,
				};

				this.TransactionsMutable.Add(tx);
			}

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
}
