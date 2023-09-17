// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Runtime.InteropServices;
using uniffi.LightWallet;

namespace Nerdbank.Zcash;

/// <summary>
/// Exposes functionality of a lightwallet client.
/// </summary>
public class LightWalletClient : IDisposable
{
	/// <summary>
	/// The number of ZATs in a ZEC.
	/// </summary>
	internal const uint ZatsPerZEC = 100_000_000;

	private readonly Uri serverUrl;
	private readonly ZcashAccount? account;
	private readonly LightWalletSafeHandle handle;

	/// <summary>
	/// Initializes a new instance of the <see cref="LightWalletClient"/> class.
	/// </summary>
	/// <param name="serverUrl">The URL of a lightwallet server to use.</param>
	/// <param name="account">The account whose keys will be used with this server.</param>
	/// <param name="walletPath">The absolute path to the directory where the wallet and log will be written.</param>
	/// <param name="walletName">The filename of the wallet (without a path).</param>
	/// <param name="logName">The filename of the log file (without a path).</param>
	/// <param name="watchMemPool">A value indicating whether the mempool will be monitored.</param>
	public LightWalletClient(Uri serverUrl, ZcashAccount account, string walletPath, string walletName, string logName, bool watchMemPool)
	{
		Requires.NotNull(serverUrl);
		Requires.NotNull(account);

		if (account.FullViewing is null)
		{
			throw new NotSupportedException("This lightwallet client does not support wallets with only incoming viewing keys.");
		}

		this.serverUrl = serverUrl;
		this.account = account;

		Span<byte> uskBytes = stackalloc byte[500];
		int uskBytesLength = account.Spending?.UnifiedKey.ToBytes(uskBytes) ?? 0;
		List<byte>? uskBytesList = uskBytesLength == 0 ? null : ToByteList(uskBytes[..uskBytesLength]);

		WalletInfo walletInfo = new(
			account.Spending is null ? account.FullViewing.UnifiedKey : null,
			uskBytesList,
			account.BirthdayHeight ?? 0);

		this.handle = new LightWalletSafeHandle(
			unchecked((nint)LightWalletMethods.LightwalletInitialize(
				new Config(
					serverUrl.AbsoluteUri,
					ToChainType(account.Network),
					walletPath,
					walletName,
					logName,
					watchMemPool),
				walletInfo)),
			ownsHandle: true);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="LightWalletClient"/> class
	/// from an existing wallet file.
	/// </summary>
	/// <param name="serverUrl">The URL of a lightwallet server to use.</param>
	/// <param name="network">The network the wallet operates on.</param>
	/// <param name="walletPath">The absolute path to the directory where the wallet and log will be written.</param>
	/// <param name="walletName">The filename of the wallet (without a path).</param>
	/// <param name="logName">The filename of the log file (without a path).</param>
	/// <param name="watchMemPool">A value indicating whether the mempool will be monitored.</param>
	public LightWalletClient(Uri serverUrl, ZcashNetwork network, string walletPath, string walletName, string logName, bool watchMemPool)
	{
		Requires.NotNull(serverUrl);

		this.serverUrl = serverUrl;

		this.handle = new LightWalletSafeHandle(
			unchecked((nint)LightWalletMethods.LightwalletInitializeFromDisk(
				new Config(
					serverUrl.AbsoluteUri,
					ToChainType(network),
					walletPath,
					walletName,
					logName,
					watchMemPool))),
			ownsHandle: true);
	}

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
	public ulong BirthdayHeight => this.Interop(LightWalletMethods.LightwalletGetBirthdayHeight);

	/// <summary>
	/// Gets the block last downloaded from the blockchain.
	/// </summary>
	public ulong LastDownloadHeight => this.Interop(LightWalletMethods.LastSyncedHeight);

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
			cancellationToken);

		return new SyncResult(result);
	}

	/// <summary>
	/// Gets all the downloaded transactions for this account included in a given block or later.
	/// </summary>
	/// <param name="startingBlock">The minimum block number to return transactions for.</param>
	/// <returns>A list of transactions.</returns>
	public List<Transaction> GetDownloadedTransactions(uint startingBlock = 0)
	{
		return this.Interop(h => LightWalletMethods.LightwalletGetTransactions(h, startingBlock))
			.Select(t => new Transaction(t))
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
	public async Task<string> SendAsync(IReadOnlyCollection<TransactionSendItem> payments, IProgress<SendProgress>? progress, CancellationToken cancellationToken)
	{
		Requires.NotNullOrEmpty(payments);

		List<TransactionSendDetail> details = payments
			.Select(p => new TransactionSendDetail(p.ToAddress, (ulong)(p.Amount * ZatsPerZEC), null, p.Memo.RawBytes.ToArray().ToList()))
			.ToList();

		return await this.InteropAsync(
			h => LightWalletMethods.LightwalletSendToAddress(h, details),
			progress,
			h => new SendProgress(LightWalletMethods.LightwalletSendCheckStatus(h)),
			cancellationToken);
	}

	/// <summary>
	/// Gets pool balances.
	/// </summary>
	/// <returns>Pool balances.</returns>
	public PoolBalances GetPoolBalances() => new(this.Interop(LightWalletMethods.LightwalletGetBalances));

	/// <inheritdoc/>
	public void Dispose()
	{
		this.handle.Dispose();
	}

	private static decimal ZatsToZEC(ulong zats) => (decimal)zats / ZatsPerZEC;

	private static ChainType ToChainType(ZcashNetwork network)
	{
		return network switch
		{
			ZcashNetwork.MainNet => ChainType.MAINNET,
			ZcashNetwork.TestNet => ChainType.TESTNET,
			_ => throw new ArgumentException(),
		};
	}

	private static List<byte> ToByteList(ReadOnlySpan<byte> buffer)
	{
		List<byte> list = new(buffer.Length);
		for (int i = 0; i < buffer.Length; i++)
		{
			list.Add(buffer[i]);
		}

		return list;
	}

	private async ValueTask<T> InteropAsync<T, TProgress>(Func<ulong, T> func, IProgress<TProgress>? progress, Func<ulong, TProgress> checkProgress, CancellationToken cancellationToken)
	{
		using (this.TrackProgress(progress, checkProgress))
		{
			return await this.InteropAsync(func, cancellationToken);
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
				await Task.Delay(this.UpdateFrequency, cts.Token);
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
	/// Describes an individual spend in a transaction.
	/// </summary>
	/// <param name="Amount">The amount spent.</param>
	/// <param name="ToAddress">The receiver of this ZEC.</param>
	/// <param name="RecipientUA">The full UA that was used when spending this, as recorded in the private change memo.</param>
	/// <param name="Memo">The memo included for this recipient.</param>
	public record struct TransactionSendItem(ZcashAddress ToAddress, decimal Amount, in Memo Memo, UnifiedAddress? RecipientUA = null)
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="TransactionSendItem"/> class.
		/// </summary>
		/// <param name="d">The uniffi data to copy from.</param>
		internal TransactionSendItem(TransactionSendDetail d)
			: this(ZcashAddress.Parse(d.toAddress), ZatsToZEC(d.value), new Memo(d.memo.ToArray()), d.recipientUa is null ? null : (UnifiedAddress)ZcashAddress.Parse(d.recipientUa))
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
	public record SyncProgress(
		string? LastError,
		ulong StartBlock,
		ulong EndBlock,
		ulong BlocksDone,
		ulong TxnScanDone,
		ulong BlocksTotal,
		ulong BatchNum,
		ulong BatchTotal)
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="SyncProgress"/> class
		/// based on data coming from the native code.
		/// </summary>
		/// <param name="copyFrom">The data to copy from.</param>
		internal SyncProgress(SyncStatus copyFrom)
			: this(copyFrom.lastError, copyFrom.startBlock, copyFrom.endBlock, copyFrom.blocksDone, copyFrom.txnScanDone, copyFrom.blocksTotal, copyFrom.batchNum, copyFrom.batchTotal)
		{
		}
	}

	/// <summary>
	/// Describes a Zcash transaction.
	/// </summary>
	/// <param name="TransactionId">The transaction ID.</param>
	/// <param name="BlockNumber">The block that mined this transaction.</param>
	/// <param name="When">The timestamp on this transaction.</param>
	/// <param name="IsUnconfirmed">A value indicating whether this transaction is stil waiting in the mempool, unmined.</param>
	/// <param name="Spent">The amount of ZEC that was spent in this transaction.</param>
	/// <param name="Received">The amount of ZEC that was received in this transaction.</param>
	/// <param name="Sends">A collection of individual spend details with amounts and recipients belonging to this transaction.</param>
	public record Transaction(string TransactionId, uint BlockNumber, DateTime When, bool IsUnconfirmed, decimal Spent, decimal Received, ImmutableArray<TransactionSendItem> Sends)
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="Transaction"/> class.
		/// </summary>
		/// <param name="t">The uniffi transaction to copy data from.</param>
		internal Transaction(uniffi.LightWallet.Transaction t)
			: this(t.txid, t.blockHeight, DateTime.UnixEpoch.AddSeconds(t.datetime), t.unconfirmed, ZatsToZEC(t.spent), ZatsToZEC(t.received), t.sends.Select(s => new TransactionSendItem(s)).ToImmutableArray())
		{
		}

		/// <summary>
		/// Gets the net balance change applied by this transaction.
		/// </summary>
		public decimal NetChange => this.Received - this.Spent;
	}

	/// <summary>
	/// The balances that apply to a single shielded pool.
	/// </summary>
	/// <param name="Balance">The pool balance.</param>
	/// <param name="VerifiedBalance">The verified balance.</param>
	/// <param name="UnverifiedBalance">The unverified balance.</param>
	/// <param name="SpendableBalance">The spendable balance.</param>
	public record ShieldedPoolBalance(decimal Balance, decimal VerifiedBalance, decimal UnverifiedBalance, decimal SpendableBalance)
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
	/// <param name="TransparentBalance">The transparent balance, in ZEC.</param>
	/// <param name="Sapling">The sapling balance.</param>
	/// <param name="Orchard">The orchard balance.</param>
	public record PoolBalances(decimal? TransparentBalance, ShieldedPoolBalance? Sapling, ShieldedPoolBalance? Orchard)
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="PoolBalances"/> class.
		/// </summary>
		/// <param name="balances">The balances as they come from the interop layer.</param>
		internal PoolBalances(uniffi.LightWallet.PoolBalances balances)
			: this(
				Requires.NotNull(balances).transparentBalance.HasValue ? ZatsToZEC(balances.transparentBalance!.Value) : null,
				balances.saplingBalance.HasValue ? new(balances.saplingBalance.Value, balances.verifiedSaplingBalance!.Value, balances.unverifiedSaplingBalance!.Value, balances.spendableSaplingBalance!.Value) : null,
				balances.orchardBalance.HasValue ? new(balances.orchardBalance.Value, balances.verifiedOrchardBalance!.Value, balances.unverifiedOrchardBalance!.Value, balances.spendableOrchardBalance!.Value) : null)
		{
		}
	}

	/// <summary>
	/// A <see cref="SafeHandle"/> that contains the handle to the native lightwallet used by
	/// an instance of <see cref="LightWalletClient"/>.
	/// </summary>
	private class LightWalletSafeHandle : SafeHandle
	{
		internal LightWalletSafeHandle(nint invalidHandleValue, bool ownsHandle)
			: base(invalidHandleValue, ownsHandle)
		{
		}

		public override bool IsInvalid => this.handle <= 0;

		protected override bool ReleaseHandle() => LightWalletMethods.LightwalletDeinitialize((ulong)this.handle);
	}
}
