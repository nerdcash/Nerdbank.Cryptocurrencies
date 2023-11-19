// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.ComponentModel;
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
	private decimal spendableBalance;
	private decimal unconfirmedBalance;
	private bool isDirty;
	private LightWalletClient.SyncProgress? syncProgress;
	private uint lastBlockHeight;
	private ObservableCollection<ZcashTransaction> transactionsMutable = new();
	private decimal anticipatedFees;
	private decimal unspendableChange;
	private decimal immatureIncome;

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

	/// <summary>
	/// Gets or sets the sum of <see cref="SpendableBalance"/>, <see cref="UnspendableChange"/>, <see cref="ImmatureIncome"/> and the (negative) <see cref="AnticipatedFees"/> values.
	/// </summary>
	[IgnoreMember]
	public decimal Balance
	{
		get => this.balance;
		set => this.RaiseAndSetIfChanged(ref this.balance, value);
	}

	/// <summary>
	/// Gets or sets the sum of all unspent notes that have sufficient confirmations to be spent.
	/// </summary>
	/// <remarks>
	/// For enhanced privacy, the minimum number of required confirmations to spend a note is usually greater than one.
	/// </remarks>
	[IgnoreMember]
	public decimal SpendableBalance
	{
		get => this.spendableBalance;
		set => this.RaiseAndSetIfChanged(ref this.spendableBalance, value);
	}

	/// <summary>
	/// Gets or sets the amount of the user's own funds that are unspendable until the change note that carries them
	/// has sufficient confirmations to move back into <see cref="SpendableBalance"/>.
	/// </summary>
	[IgnoreMember]
	public decimal UnspendableChange
	{
		get => this.unspendableChange;
		set => this.RaiseAndSetIfChanged(ref this.unspendableChange, value);
	}

	/// <summary>
	/// Gets or sets the sum of all UTXOs or under-confirmed (shielded) notes.
	/// </summary>
	/// <remarks>
	/// Transparent funds are always in this category, awaiting auto-shielding.
	/// Shielded funds may also appear in this category until they have sufficient confirmations to qualify as <see cref="SpendableBalance"/>.
	/// </remarks>
	[IgnoreMember]
	public decimal ImmatureIncome
	{
		get => this.immatureIncome;
		set => this.RaiseAndSetIfChanged(ref this.immatureIncome, value);
	}

	/// <summary>
	/// Gets or sets the minimum amount that <em>must</em> be spent in fees to spend the <see cref="Balance"/>.
	/// </summary>
	[IgnoreMember]
	public decimal AnticipatedFees
	{
		get => this.anticipatedFees;
		set => this.RaiseAndSetIfChanged(ref this.anticipatedFees, value);
	}

	/// <summary>
	/// Gets or sets the amount of incoming funds that are in the mempool, excluding change notes counted by <see cref="UnspendableChange"/>.
	/// </summary>
	[IgnoreMember]
	public decimal UnconfirmedBalance
	{
		get => this.unconfirmedBalance;
		set => this.RaiseAndSetIfChanged(ref this.unconfirmedBalance, value);
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

	public void AddTransactions(IEnumerable<LightWalletClient.Transaction> transactions, uint? upToBlockNumber)
	{
		uint highestBlockNumber = 0;
		foreach (LightWalletClient.Transaction transaction in transactions)
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
					Amount = this.Network.AsSecurity().Amount(transaction.NetChange),
					//Memo = transaction.Notes[0].Memo,
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
}
