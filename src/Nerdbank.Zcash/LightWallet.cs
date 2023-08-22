// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using uniffi.LightWallet;

namespace Nerdbank.Zcash;

/// <summary>
/// Exposes functionality of a lightwallet client.
/// </summary>
public class LightWallet : IDisposableObservable
{
	private readonly Uri serverUrl;
	private readonly ZcashNetwork network;
	private readonly LightWalletSafeHandle handle;
	private bool disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="LightWallet"/> class.
	/// </summary>
	/// <param name="serverUrl">The URL of a lightwallet server to use.</param>
	/// <param name="network">The Zcash network served by the server specified by <paramref name="serverUrl"/>.</param>
	/// <param name="walletPath">The absolute path to the directory where the wallet and log will be written.</param>
	/// <param name="walletName">The filename of the wallet (without a path).</param>
	/// <param name="logName">The filename of the log file (without a path).</param>
	/// <param name="watchMemPool">A value indicating whether the mempool will be monitored.</param>
	public LightWallet(Uri serverUrl, ZcashNetwork network, string walletPath, string walletName, string logName, bool watchMemPool)
	{
		Requires.NotNull(serverUrl);

		this.serverUrl = serverUrl;
		this.network = network;

		this.handle = new LightWalletSafeHandle(
			unchecked((nint)LightWalletMethods.LightwalletInitialize(
				new Config(
				serverUrl.AbsoluteUri,
				ToNetwork(network),
				walletPath,
				walletName,
				logName,
				watchMemPool))),
			ownsHandle: true);
	}

	/// <inheritdoc/>
	bool IDisposableObservable.IsDisposed => this.disposed;

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

	public ulong BirthdayHeight => this.Interop(LightWalletMethods.LightwalletGetBirthdayHeight);

	public async Task<string> DownloadTransactionsAsync(IProgress<SyncProgress> progress, TimeSpan updateFrequency, CancellationToken cancellationToken)
	{
		using CancellationTokenRegistration ctr = cancellationToken.Register(() =>
		{
			this.Interop(h => LightWalletMethods.LightwalletSyncInterrupt(h));
		});

		// Set up a timer by which we will check on progress and report back.
		bool inProgress = true;
		_ = Task.Run(async delegate
		{
			while (inProgress)
			{
				await Task.Delay(updateFrequency, cancellationToken);
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

		try
		{
			string result = await this.InteropAsync(LightWalletMethods.LightwalletSync, cancellationToken);

			// TODO: Consider throwing OCE if canceled, and we haven't reach the tip of the blockchain yet,
			// suggesting that we did indeed cancel mid-sync.
			return result;
		}
		finally
		{
			inProgress = false;
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

	public void StopSync()
	{
		// interrupt_sync_after_batch is available to request stopping at a convenient point.
		throw new NotImplementedException();
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		this.disposed = true;
		this.handle.Dispose();
	}

	private static Network ToNetwork(ZcashNetwork network)
	{
		return network switch
		{
			ZcashNetwork.MainNet => Network.MAIN_NET,
			ZcashNetwork.TestNet => Network.TEST_NET,
			_ => throw new ArgumentException(),
		};
	}

	public record SyncProgress(
		bool InProgress,
		string? LastError,
		ulong SyncId,
		ulong StartBlock,
		ulong EndBlock,
		ulong BlocksDone,
		ulong TrialDecDone,
		ulong TxnScanDone,
		ulong BlocksTotal,
		ulong BatchNum,
		ulong BatchTotal)
	{
		internal SyncProgress(SyncStatus copyFrom)
			: this(copyFrom.inProgress, copyFrom.lastError, copyFrom.syncId, copyFrom.startBlock, copyFrom.endBlock, copyFrom.blocksDone, copyFrom.trialDecDone, copyFrom.txnScanDone, copyFrom.blocksTotal, copyFrom.batchNum, copyFrom.batchTotal)
		{
		}
	}

	internal class LightWalletSafeHandle : SafeHandle
	{
		public LightWalletSafeHandle(nint invalidHandleValue, bool ownsHandle)
			: base(invalidHandleValue, ownsHandle)
		{
		}

		public override bool IsInvalid => this.handle <= 0;

		protected override bool ReleaseHandle() => LightWalletMethods.LightwalletDeinitialize((ulong)this.handle);
	}
}
