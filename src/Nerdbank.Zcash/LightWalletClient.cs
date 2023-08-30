// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using uniffi.LightWallet;

namespace Nerdbank.Zcash;

/// <summary>
/// Exposes functionality of a lightwallet client.
/// </summary>
public class LightWalletClient : IDisposableObservable
{
	private readonly Uri serverUrl;
	private readonly ZcashWallet.Account account;
	private readonly LightWalletSafeHandle handle;
	private bool disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="LightWalletClient"/> class.
	/// </summary>
	/// <param name="serverUrl">The URL of a lightwallet server to use.</param>
	/// <param name="account">The account whose keys will be used with this server.</param>
	/// <param name="walletPath">The absolute path to the directory where the wallet and log will be written.</param>
	/// <param name="walletName">The filename of the wallet (without a path).</param>
	/// <param name="logName">The filename of the log file (without a path).</param>
	/// <param name="watchMemPool">A value indicating whether the mempool will be monitored.</param>
	public LightWalletClient(Uri serverUrl, ZcashWallet.Account account, string walletPath, string walletName, string logName, bool watchMemPool)
	{
		Requires.NotNull(serverUrl);
		Requires.NotNull(account);

		this.serverUrl = serverUrl;
		this.account = account;

		this.handle = new LightWalletSafeHandle(
			unchecked((nint)LightWalletMethods.LightwalletInitialize(
				new Config(
				serverUrl.AbsoluteUri,
				ToNetwork(account.Network),
				walletPath,
				walletName,
				logName,
				watchMemPool))),
			ownsHandle: true);
	}

	/// <inheritdoc/>
	bool IDisposableObservable.IsDisposed => this.disposed;

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
	/// Gets the height of the blockchain (independent of what may have been sync'd thus far.)
	/// </summary>
	/// <param name="lightWalletServerUrl">The URL of the lightwallet server to query.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The height of the blockchain.</returns>
	/// <exception cref="LightWalletException">Thrown if any error occurs.</exception>
	public static ValueTask<ulong> GetLatestBlockHeightAsync(Uri lightWalletServerUrl, CancellationToken cancellationToken)
	{
		return new(Task.Run(
			() => LightWalletMethods.LightwalletGetBlockHeight(lightWalletServerUrl.AbsoluteUri),
			cancellationToken));
	}

	/// <inheritdoc cref="GetLatestBlockHeightAsync(Uri, CancellationToken)"/>
	public ValueTask<ulong> GetLatestBlockHeightAsync(CancellationToken cancellationToken) => GetLatestBlockHeightAsync(this.serverUrl, cancellationToken);

	/// <inheritdoc cref="DownloadTransactionsAsync(IProgress{SyncProgress}?, TimeSpan?, CancellationToken)"/>
	public Task<SyncResult> DownloadTransactionsAsync(CancellationToken cancellationToken) => this.DownloadTransactionsAsync(null, null, cancellationToken);

	/// <summary>
	/// Scans the blockchain for new transactions related to this account.
	/// </summary>
	/// <param name="progress">
	/// An optional receiver of updates that report on progress toward the tip of the blockchain.
	/// Because scanning the blockchain may take a <em>very</em> long time, reporting progress to the user is <em>highly</em> recommended.
	/// </param>
	/// <param name="updateFrequency">
	/// The interval between each <paramref name="progress"/> update.
	/// If <see langword="null"/>, a reasonable default will be used.
	/// Irrelevant if <paramref name="progress"/> is <see langword="null" />.
	/// </param>
	/// <param name="cancellationToken">
	/// A cancellation token.
	/// There may be a substantial delay between cancellation and when the blockchain scan is suspended in order to conclude work on the current 'batch'.
	/// Cancellation does <em>not</em> result in an <see cref="OperationCanceledException"/> being thrown,
	/// but rather concludes the operation early with an indication of the last block that was scanned.
	/// </param>
	/// <returns>A task with the result of the scan.</returns>
	public async Task<SyncResult> DownloadTransactionsAsync(IProgress<SyncProgress>? progress, TimeSpan? updateFrequency, CancellationToken cancellationToken)
	{
		using CancellationTokenRegistration ctr = cancellationToken.Register(() =>
		{
			this.Interop(h => LightWalletMethods.LightwalletSyncInterrupt(h));
		});

		bool inProgress = true;
		if (progress is not null)
		{
			// Set up a timer by which we will check on progress and report back.
			_ = Task.Run(async delegate
			{
				updateFrequency ??= TimeSpan.FromSeconds(1);
				while (inProgress)
				{
					await Task.Delay(updateFrequency.Value, cancellationToken);
					SyncStatus status = this.Interop(h => LightWalletMethods.LightwalletSyncStatus(h));
					progress.Report(new SyncProgress(status));
					if (!status.inProgress)
					{
						// Another indicator that there is nothing more to check on occurred.
						// Theoretically we should break from inProgress,
						// but might as well make sure we don't loop around do to timing,
						// and update our caller after progress has stopped.
						break;
					}
				}
			});
		}

		try
		{
			uniffi.LightWallet.SyncResult result = await this.InteropAsync(LightWalletMethods.LightwalletSync, cancellationToken);
			return new SyncResult(result);
		}
		finally
		{
			inProgress = false;
		}
	}

	/// <summary>
	/// Gets all the downloaded transactions for this account included in a given block or later.
	/// </summary>
	/// <param name="startingBlock">The minimum block number to return transactions for.</param>
	/// <returns>A list of transactions.</returns>
	public List<Transaction> GetDownloadedTransactions(uint startingBlock = 0)
	{
		return this.Interop(h => LightWalletMethods.LightwalletGetTransactions(h, startingBlock));
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		this.disposed = true;
		this.handle.Dispose();
	}

	private static ChainType ToNetwork(ZcashNetwork network)
	{
		return network switch
		{
			ZcashNetwork.MainNet => ChainType.MAINNET,
			ZcashNetwork.TestNet => ChainType.TESTNET,
			_ => throw new ArgumentException(),
		};
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
	/// The data in a periodic status report during a <see cref="DownloadTransactionsAsync(IProgress{SyncProgress}?, TimeSpan?, CancellationToken)"/> operation.
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
