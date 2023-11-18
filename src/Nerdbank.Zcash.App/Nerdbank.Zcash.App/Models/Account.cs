// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using MessagePack;
using Nerdbank.Cryptocurrencies.Exchanges;

namespace Nerdbank.Zcash.App.Models;

[MessagePackObject]
public class Account : ReactiveObject, IPersistableData
{
	private readonly ObservableAsPropertyHelper<SecurityAmount> securityBalance;
	private string? zingoWalletFileName;
	private string name = string.Empty;
	private decimal balance;
	private bool isDirty;
	private LightWalletClient.SyncProgress? syncProgress;
	private uint lastBlockHeight;
	private ObservableCollection<ZcashTransaction> transactionsMutable = new();

	public Account(ZcashAccount account)
	{
		this.ZcashAccount = account;

		this.securityBalance = this.WhenAnyValue(
			vm => vm.Balance,
			balance => this.Network.AsSecurity().Amount(balance))
			.ToProperty(this, nameof(this.SecurityBalance));

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

	[IgnoreMember]
	public decimal Balance
	{
		get => this.balance;
		set => this.RaiseAndSetIfChanged(ref this.balance, value);
	}

	[IgnoreMember]
	public SecurityAmount SecurityBalance => this.securityBalance.Value;

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

	internal void AddTransactions(IEnumerable<LightWalletClient.Transaction> transactions, uint? upToBlockNumber)
	{
		uint highestBlockNumber = 0;
		foreach (LightWalletClient.Transaction transaction in transactions)
		{
			// TODO: support transactions coming in multiple times.
			// Transactions in the mempool may appear multiple times.
			if (!this.TransactionsMutable.Any(t => t.TransactionId == transaction.TransactionId))
			{
				ZcashTransaction tx = new ZcashTransaction
				{
					BlockNumber = transaction.IsUnconfirmed ? null : transaction.BlockNumber,
					TransactionId = transaction.TransactionId,
					IsIncoming = transaction.IsIncoming,
					When = transaction.When,
					Amount = this.Network.AsSecurity().Amount(transaction.NetChange),
					//Memo = transaction.Notes[0].Memo,
				};

				highestBlockNumber = Math.Max(highestBlockNumber, tx.BlockNumber ?? 0);
				this.TransactionsMutable.Add(tx);
			}
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
}
