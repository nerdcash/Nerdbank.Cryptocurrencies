// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using uniffi.LightWallet;
using static Nerdbank.Zcash.Transaction;
using static Nerdbank.Zcash.ZcashUtilities;

namespace Nerdbank.Zcash;

/// <summary>
/// Exposes functionality of a lightwallet client.
/// </summary>
public partial class LightWalletClient : IDisposable
{
	/// <summary>
	/// The number of confirmations required to spend a note.
	/// </summary>
	/// <remarks>
	/// While only one confirmation is technically required by the protocol,
	/// more confirmations make reorgs less likely to invalidate a transaction that spends a note.
	/// Since spending a note requires revealing a nullifier, more than one attempt to spend a note
	/// allows folks who are watching the mempool to detect that the same spender is behind each attempt.
	/// </remarks>
	public const int MinimumConfirmations = 3;

	private readonly Uri serverUrl;
	private readonly ZcashAccount? account;
	private readonly DbInit dbinit;

	/// <summary>
	/// Initializes a new instance of the <see cref="LightWalletClient"/> class.
	/// </summary>
	/// <param name="serverUrl">The URL of a lightwallet server to use.</param>
	/// <param name="network">The network the wallet operates on.</param>
	/// <param name="dataFile">The path to the sqlite database to load (or create) that stores the decrypted transactions.</param>
	public LightWalletClient(Uri serverUrl, ZcashNetwork network, string dataFile)
	{
		Requires.NotNull(serverUrl);

		this.dbinit = new(dataFile, ToChainType(network));
		this.serverUrl = serverUrl;
		this.Network = network;

		LightWalletMethods.LightwalletInit(this.dbinit);
	}

	/// <summary>
	/// Gets the Zcash network that this client operates on.
	/// </summary>
	public ZcashNetwork Network { get; }

	/// <summary>
	/// Gets or sets the interval to report progress for long-running tasks.
	/// </summary>
	public TimeSpan UpdateFrequency { get; set; } = TimeSpan.FromSeconds(1);

	/// <summary>
	/// Gets the birthday height of the account.
	/// </summary>
	/// <remarks>
	/// The birthday height is the length of the blockchain when the account was created.
	/// It serves to reduce initial sync times when this account is imported into another wallet.
	/// </remarks>
	/// <seealso cref="GetBirthdayHeights"/>
	public uint? BirthdayHeight => LightWalletMethods.LightwalletGetBirthdayHeight(this.dbinit);

	/// <summary>
	/// Gets the block last downloaded from the blockchain.
	/// </summary>
	public uint? LastDownloadHeight => LightWalletMethods.LightwalletGetSyncHeight(this.dbinit);

	public void AddSpendingKey(ZcashAccount account)
	{
		Span<byte> uskBytes = stackalloc byte[500];
		int uskBytesLength = account.Spending?.UnifiedKey.ToBytes(uskBytes) ?? 0;
		byte[]? uskBytesList = uskBytesLength == 0 ? null : uskBytes[..uskBytesLength].ToArray();
	}

	/// <summary>
	/// Gets the length of the blockchain (independent of what may have been sync'd thus far.)
	/// </summary>
	/// <param name="lightWalletServerUrl">The URL of the lightwallet server to query.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The length of the blockchain.</returns>
	/// <exception cref="LightWalletException">Thrown if any error occurs.</exception>
	public static ValueTask<ulong> GetLatestBlockHeightAsync(Uri lightWalletServerUrl, CancellationToken cancellationToken)
	{
		return new(Task.Run(
			() => LightWalletMethods.LightwalletGetBlockHeight(lightWalletServerUrl.AbsoluteUri),
			cancellationToken));
	}

	/// <summary>
	/// Gets the various forms of birthday heights relevant to this account.
	/// </summary>
	/// <returns>The birthday heights.</returns>
	/// <remarks>
	/// The resulting struct contains fields which may be influenced by the completeness of the sync to the blockchain.
	/// </remarks>
	public BirthdayHeights GetBirthdayHeights() => this.Interop(LightWalletMethods.LightwalletGetBirthdayHeights);

	/// <inheritdoc cref="GetLatestBlockHeightAsync(Uri, CancellationToken)"/>
	public ValueTask<ulong> GetLatestBlockHeightAsync(CancellationToken cancellationToken) => GetLatestBlockHeightAsync(this.serverUrl, cancellationToken);

	/// <inheritdoc cref="DownloadTransactionsAsync(IProgress{SyncProgress}?, CancellationToken)"/>
	public Task<SyncResult> DownloadTransactionsAsync(CancellationToken cancellationToken) => this.DownloadTransactionsAsync(null, cancellationToken);

	/// <summary>
	/// Scans the blockchain for new transactions related to this account.
	/// </summary>
	/// <param name="progress">
	/// An optional receiver of updates that report on progress toward the tip of the blockchain.
	/// Because scanning the blockchain may take a <em>very</em> long time, reporting progress to the user is <em>highly</em> recommended.
	/// </param>
	/// <param name="cancellationToken">
	/// A cancellation token.
	/// There may be a substantial delay between cancellation and when the blockchain scan is suspended in order to conclude work on the current 'batch'.
	/// Cancellation does <em>not</em> result in an <see cref="OperationCanceledException"/> being thrown,
	/// but rather concludes the operation early with an indication of the last block that was scanned.
	/// </param>
	/// <returns>A task with the result of the scan.</returns>
	public async Task<SyncResult> DownloadTransactionsAsync(IProgress<SyncProgress>? progress, CancellationToken cancellationToken)
	{
		using CancellationTokenRegistration ctr = cancellationToken.Register(() =>
		{
			this.Interop(h => LightWalletMethods.LightwalletSyncInterrupt(h));
		});

		uniffi.LightWallet.SyncResult result = await this.InteropAsync(
			LightWalletMethods.LightwalletSync,
			progress,
			h => new SyncProgress(LightWalletMethods.LightwalletSyncStatus(h)),
			cancellationToken).ConfigureAwait(false);

		return new SyncResult(result);
	}

	/// <summary>
	/// Gets all the downloaded transactions for this account included in a given block or later.
	/// </summary>
	/// <param name="startingBlock">The minimum block number to return transactions for.</param>
	/// <returns>A list of transactions.</returns>
	public List<Transaction> GetDownloadedTransactions(uint startingBlock = 0)
	{
		return LightWalletMethods.LightwalletGetTransactions(this.dbinit, 1, startingBlock)
			.Select(t => CreateTransaction(t, this.Network))
			.OrderBy(t => t.When)
			.ToList();
	}

	/// <summary>
	/// Creates and broadcasts a transaction that sends Zcash to one or more recipients.
	/// </summary>
	/// <param name="payments">The payments to be made.</param>
	/// <param name="progress">An optional receiver for progress updates.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The transaction ID.</returns>
	public async Task<string> SendAsync(IReadOnlyCollection<SendItem> payments, IProgress<SendProgress>? progress, CancellationToken cancellationToken)
	{
		Requires.NotNullOrEmpty(payments);

		List<TransactionSendDetail> details = payments
			.Select(p => new TransactionSendDetail(p.ToAddress, (ulong)(p.Amount * ZatsPerZEC), null, p.Memo.RawBytes.ToArray()))
			.ToList();

		return await this.InteropAsync(
			h => LightWalletMethods.LightwalletSendToAddress(h, details),
			progress,
			h => new SendProgress(LightWalletMethods.LightwalletSendCheckStatus(h)),
			cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Gets pool balances.
	/// </summary>
	/// <returns>Pool balances.</returns>
	public PoolBalances GetPoolBalances() => new(this.Interop(LightWalletMethods.LightwalletGetBalances));

	/// <summary>
	/// Gets user balances.
	/// </summary>
	/// <returns>Pool balances.</returns>
	public AccountBalances GetUserBalances() => new(this.Network.AsSecurity(), this.Interop(LightWalletMethods.LightwalletGetUserBalances));

	/// <inheritdoc/>
	public void Dispose()
	{
		this.handle.Dispose();
	}

	private static ChainType ToChainType(ZcashNetwork network)
	{
		return network switch
		{
			ZcashNetwork.MainNet => ChainType.Mainnet,
			ZcashNetwork.TestNet => ChainType.Testnet,
			_ => throw new ArgumentException(),
		};
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SendItem"/> class.
	/// </summary>
	/// <param name="d">The uniffi data to copy from.</param>
	private static SendItem CreateSendItem(TransactionSendDetail d)
		=> new(ZcashAddress.Decode(d.toAddress), ZatsToZEC(d.value), new Memo(d.memo.ToArray()));

	/// <summary>
	/// Initializes a new instance of the <see cref="RecvItem"/> class.
	/// </summary>
	/// <param name="d">The uniffi sapling note.</param>
	/// <param name="network">The network that the transaction is on.</param>
	private static RecvItem CreateRecvItem(SaplingNote d, ZcashNetwork network)
		=> new(new SaplingAddress(new SaplingReceiver(d.recipient.ToArray()), network), ZatsToZEC(d.value), new Memo(d.memo.ToArray()), d.isChange);

	/// <summary>
	/// Initializes a new instance of the <see cref="RecvItem"/> class.
	/// </summary>
	/// <param name="d">The uniffi orchard note.</param>
	/// <param name="network">The network that the transaction is on.</param>
	private static RecvItem CreateRecvItem(OrchardNote d, ZcashNetwork network)
		=> new(new OrchardAddress(new OrchardReceiver(d.recipient.ToArray()), network), ZatsToZEC(d.value), new Memo(d.memo.ToArray()), d.isChange);

	/// <summary>
	/// Initializes a new instance of the <see cref="Transaction"/> class.
	/// </summary>
	/// <param name="t">The uniffi transaction to copy data from.</param>
	/// <param name="network">The network this transaction is on.</param>
	private static Transaction CreateTransaction(uniffi.LightWallet.Transaction t, ZcashNetwork network)
		=> new(t.txid, t.minedHeight, t.expiredUnmined, t.blockTime, ZatsToZEC(t.accountBalanceDelta), ZatsToZEC(t.fee), t.outgoing.Select(s => CreateSendItem(s)).ToImmutableArray(), t.incomingSapling.Select(s => CreateRecvItem(s, network)).Concat(t.incomingOrchard.Select(o => CreateRecvItem(o, network))).ToImmutableArray());

	private async ValueTask<T> InteropAsync<T, TProgress>(Func<ulong, T> func, IProgress<TProgress>? progress, Func<ulong, TProgress> checkProgress, CancellationToken cancellationToken)
	{
		using (this.TrackProgress(progress, checkProgress))
		{
			return await this.InteropAsync(func, cancellationToken).ConfigureAwait(false);
		}
	}

	private ValueTask<T> InteropAsync<T>(Func<ulong, T> func, CancellationToken cancellationToken)
	{
		bool refAdded = false;
		try
		{
			this.handle.DangerousAddRef(ref refAdded);
			return new(Task.Run(
				delegate
				{
					return func((ulong)this.handle.DangerousGetHandle());
				},
				cancellationToken));
		}
		finally
		{
			if (refAdded)
			{
				this.handle.DangerousRelease();
			}
		}
	}

	private T Interop<T>(Func<ulong, T> func)
	{
		bool refAdded = false;
		try
		{
			this.handle.DangerousAddRef(ref refAdded);
			return func((ulong)this.handle.DangerousGetHandle());
		}
		finally
		{
			if (refAdded)
			{
				this.handle.DangerousRelease();
			}
		}
	}

	private void Interop(Action<ulong> func)
	{
		bool refAdded = false;
		try
		{
			this.handle.DangerousAddRef(ref refAdded);
			func((ulong)this.handle.DangerousGetHandle());
		}
		finally
		{
			if (refAdded)
			{
				this.handle.DangerousRelease();
			}
		}
	}

	private IDisposable? TrackProgress<T>(IProgress<T>? progress, Func<ulong, T> fetchProgress)
	{
		if (progress is null)
		{
			return null;
		}

		CancellationTokenSource cts = new();
		bool inProgress = true;

		// Set up a timer by which we will check on progress and report back.
		_ = Task.Run(async delegate
		{
			while (inProgress)
			{
				await Task.Delay(this.UpdateFrequency, cts.Token).ConfigureAwait(false);
				T status = this.Interop(fetchProgress);
				progress.Report(status);
			}
		});

		return new DisposableAction(delegate
		{
			inProgress = false;
			cts.Cancel();
		});
	}

	/// <summary>
	/// The balances that applies to the transparent pool for a particular account.
	/// </summary>
	/// <param name="Balance">The pool balance.</param>
	public record struct TransparentPoolBalance(decimal Balance);

	/// <summary>
	/// The balances that apply to a single shielded pool for a particular account.
	/// </summary>
	/// <param name="Balance">The pool balance.</param>
	/// <param name="VerifiedBalance">The verified balance.</param>
	/// <param name="UnverifiedBalance">The unverified balance.</param>
	/// <param name="SpendableBalance">The spendable balance.</param>
	public record struct ShieldedPoolBalance(decimal Balance, decimal VerifiedBalance, decimal UnverifiedBalance, decimal SpendableBalance)
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="ShieldedPoolBalance"/> class
		/// with balances given in ZATs.
		/// </summary>
		/// <param name="balance"><inheritdoc cref="ShieldedPoolBalance(decimal, decimal, decimal, decimal)" path="/param[@name='Balance']"/></param>
		/// <param name="verified"><inheritdoc cref="ShieldedPoolBalance(decimal, decimal, decimal, decimal)" path="/param[@name='VerifiedBalance']"/></param>
		/// <param name="unverified"><inheritdoc cref="ShieldedPoolBalance(decimal, decimal, decimal, decimal)" path="/param[@name='UnverifiedBalance']"/></param>
		/// <param name="spendable"><inheritdoc cref="ShieldedPoolBalance(decimal, decimal, decimal, decimal)" path="/param[@name='SpendableBalance']"/></param>
		public ShieldedPoolBalance(ulong balance, ulong verified, ulong unverified, ulong spendable)
			: this(ZatsToZEC(balance), ZatsToZEC(verified), ZatsToZEC(unverified), ZatsToZEC(spendable))
		{
		}
	}

	/// <summary>
	/// The balance across all pools for a Zcash account.
	/// </summary>
	/// <param name="Transparent">The transparent balance.</param>
	/// <param name="Sapling">The sapling balance.</param>
	/// <param name="Orchard">The orchard balance.</param>
	public record struct PoolBalances(TransparentPoolBalance? Transparent, ShieldedPoolBalance? Sapling, ShieldedPoolBalance? Orchard)
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="PoolBalances"/> class.
		/// </summary>
		/// <param name="balances">The balances as they come from the interop layer.</param>
		internal PoolBalances(uniffi.LightWallet.PoolBalances balances)
			: this(
				Requires.NotNull(balances).transparentBalance.HasValue ? new(ZatsToZEC(balances.transparentBalance!.Value)) : null,
				balances.saplingBalance.HasValue ? new(balances.saplingBalance.Value, balances.verifiedSaplingBalance!.Value, balances.unverifiedSaplingBalance!.Value, balances.spendableSaplingBalance!.Value) : null,
				balances.orchardBalance.HasValue ? new(balances.orchardBalance.Value, balances.verifiedOrchardBalance!.Value, balances.unverifiedOrchardBalance!.Value, balances.spendableOrchardBalance!.Value) : null)
		{
		}
	}

	/// <summary>
	/// Carries details of a progress update on a send operation.
	/// </summary>
	/// <param name="Id">An id for the operation.</param>
	/// <param name="IsSendInProgress">A value indicating whether the send is in progress.</param>
	/// <param name="Progress">A value that is some proportion of the <paramref name="Total"/> value.</param>
	/// <param name="Total">Some value that <paramref name="Progress"/> is reaching for.</param>
	/// <param name="LastError">The last error to have occurred.</param>
	/// <param name="LastTransactionId">The ID of the last transaction to be created.</param>
	public record SendProgress(
		uint Id,
		bool IsSendInProgress,
		uint Progress,
		uint Total,
		string? LastError,
		string? LastTransactionId)
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="SendProgress"/> class.
		/// </summary>
		/// <param name="update">The FFI data to copy from.</param>
		internal SendProgress(SendUpdate update)
			: this(update.id, update.isSendInProgress, update.progress, update.total, update.lastError, update.lastTransactionId)
		{
		}
	}

	/// <summary>
	/// Describes the final result of a blockchain scan.
	/// </summary>
	/// <param name="Success">Indicates overall success of the operation.</param>
	/// <param name="LatestBlock">The last blocked scanned.</param>
	/// <param name="TotalBlocksScanned">The number of blocks scanned.</param>
	public record SyncResult(
		bool Success,
		ulong LatestBlock,
		ulong TotalBlocksScanned)
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="SyncResult"/> class
		/// based on data coming from the native code.
		/// </summary>
		/// <param name="copyFrom">The native data to copy from.</param>
		internal SyncResult(uniffi.LightWallet.SyncResult copyFrom)
			: this(copyFrom.success, copyFrom.latestBlock, copyFrom.totalBlocksSynced)
		{
		}
	}

	/// <summary>
	/// The data in a periodic status report during a <see cref="DownloadTransactionsAsync(IProgress{SyncProgress}?, CancellationToken)"/> operation.
	/// </summary>
	/// <param name="LastError">The last error encountered during the scan.</param>
	/// <param name="StartBlock">The number of the first block in the current batch.</param>
	/// <param name="EndBlock">The number of the last block in the current batch.</param>
	/// <param name="BlocksDone">The number of blocks scanned.</param>
	/// <param name="TxnScanDone">The number of transactions scanned.</param>
	/// <param name="BlocksTotal">The total number of blocks.</param>
	/// <param name="BatchNum">The batch number currently being scanned.</param>
	/// <param name="BatchTotal">The number of batches that the current scan operation is divided into.</param>
	/// <param name="InProgress">A value indicating whether the sync is still in progress.</param>
	public record SyncProgress(
		string? LastError,
		ulong StartBlock,
		ulong EndBlock,
		ulong BlocksDone,
		ulong TxnScanDone,
		ulong BlocksTotal,
		ulong BatchNum,
		ulong BatchTotal,
		bool InProgress)
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="SyncProgress"/> class
		/// based on data coming from the native code.
		/// </summary>
		/// <param name="copyFrom">The data to copy from.</param>
		internal SyncProgress(SyncStatus copyFrom)
			: this(copyFrom.lastError, copyFrom.startBlock, copyFrom.endBlock, copyFrom.blocksDone, copyFrom.txnScanDone, copyFrom.blocksTotal, copyFrom.batchNum, copyFrom.batchTotal, copyFrom.inProgress)
		{
		}
	}
}
