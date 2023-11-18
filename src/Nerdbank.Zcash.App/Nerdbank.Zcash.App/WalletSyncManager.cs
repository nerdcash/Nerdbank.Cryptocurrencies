// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Specialized;
using Microsoft.VisualStudio.Threading;
using Nerdbank.Zcash.App.Models;
using IAsyncDisposable = System.IAsyncDisposable;

namespace Nerdbank.Zcash.App;

public class WalletSyncManager : IAsyncDisposable
{
	private readonly string confidentialDataPath;
	private readonly ZcashWallet wallet;
	private readonly AppSettings settings;
	private readonly IContactManager contactManager;
	private Dictionary<Account, Tracker> trackers = new();
	private bool syncStarted;

	public WalletSyncManager(string confidentialDataPath, ZcashWallet wallet, AppSettings settings, IContactManager contactManager)
	{
		this.confidentialDataPath = confidentialDataPath;
		this.wallet = wallet;
		this.settings = settings;
		this.contactManager = contactManager;
	}

	public async ValueTask DisposeAsync()
	{
		INotifyCollectionChanged accounts = this.wallet.Accounts;
		accounts.CollectionChanged -= this.Wallet_CollectionChanged;
		await Task.WhenAll(this.trackers.Values.Select(t => t.DisposeAsync().AsTask()));
	}

	public void StartSyncing(ZcashWallet wallet)
	{
		Verify.Operation(!this.syncStarted, "Syncing has already started.");
		this.syncStarted = true;

		INotifyCollectionChanged accounts = wallet.Accounts;
		accounts.CollectionChanged += this.Wallet_CollectionChanged;
		foreach (Account account in wallet.Accounts)
		{
			this.trackers.Add(account, new Tracker(this, account));
		}
	}

	private void Wallet_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		if (e.NewItems is not null)
		{
			foreach (Account account in e.NewItems)
			{
				this.trackers.Add(account, new Tracker(this, account));
			}
		}

		if (e.OldItems is not null)
		{
			foreach (Account account in e.OldItems)
			{
				if (this.trackers.Remove(account, out Tracker? tracker))
				{
					tracker.DisposeAsync().Forget();
					if (account.WalletFileName is not null)
					{
						string walletFilePath = Path.Combine(this.confidentialDataPath, account.WalletFileName);
						File.Delete(walletFilePath);
					}
				}
			}
		}
	}

	private class Tracker : IAsyncDisposable
	{
		private static readonly TimeSpan SyncFrequency = TimeSpan.FromSeconds(5);
		private readonly WalletSyncManager owner;
		private LightWalletClient client;
		private CancellationTokenSource shutdownTokenSource;
		private Task syncResult;

		public Tracker(WalletSyncManager owner, Account account)
		{
			this.owner = owner;
			this.Account = account;
			this.shutdownTokenSource = new CancellationTokenSource();

			// Initialize the native wallet that will be responsible for syncing this account.
			this.client = this.CreateClient();
			account.LightWalletClient = this.client;

			// Import any transactions that we already know about.
			this.ImportTransactions(null);

			// Start the process of keeping in sync with new transactions.
			this.syncResult = this.DownloadAsync(this.shutdownTokenSource.Token);
		}

		internal Uri ServerUrl => this.owner.settings.GetLightServerUrl(this.Account.Network);

		internal Account Account { get; }

		public async ValueTask DisposeAsync()
		{
			await this.ShutdownWalletAsync();
		}

		/// <summary>
		/// Terminates sync and restarts it.
		/// </summary>
		internal async ValueTask ResetAsync()
		{
			await this.ShutdownWalletAsync();
			this.shutdownTokenSource = new CancellationTokenSource();
			this.client = this.CreateClient();
		}

		private async Task DownloadAsync(CancellationToken cancellationToken)
		{
			Progress<LightWalletClient.SyncProgress> progress = new(progress =>
			{
				// We have no idea where we are in current 'batch' (since one sync happens in many batches),
				// so for purposes of indicating which block has definitely been downloaded,
				// we'll assume we're at the end of the previous batch.
				// Oddly, EndBlock tends to be lower than StartBlock. But since I don't know why that is
				// or whether it'll stay that way, we'll just take the Min of the two.
				// We subtract one from the min because we don't know if even a single block in this batch is done.
				this.ImportTransactions(checked((uint)Math.Min(progress.EndBlock, progress.StartBlock) - 1));
				this.Account.SyncProgress = progress;
			});

			while (!cancellationToken.IsCancellationRequested)
			{
				LightWalletClient.SyncResult result = await this.client.DownloadTransactionsAsync(progress, cancellationToken);
				this.ImportTransactions(checked((uint)result.LatestBlock));
				this.Account.SyncProgress = null;

				// Either restart the sync immediately if we're already behind the tip of the chain,
				// or schedule it to start in a few seconds if we're up to date.
				ulong tip = await this.client.GetLatestBlockHeightAsync(this.shutdownTokenSource.Token);
				if (tip == result.LatestBlock)
				{
					await Task.Delay(SyncFrequency, this.shutdownTokenSource.Token);
				}
			}
		}

		private void ImportTransactions(uint? lastDownloadedBlock)
		{
			// TODO: handle re-orgs and rewrite/invalidate the necessary transactions.
			List<LightWalletClient.Transaction> txs = this.client.GetDownloadedTransactions(this.Account.LastBlockHeight + 1);
			this.Account.AddTransactions(txs, lastDownloadedBlock);
		}

		/// <summary>
		/// Shuts down the native wallet.
		/// </summary>
		private async ValueTask ShutdownWalletAsync()
		{
			await this.shutdownTokenSource.CancelAsync();
			if (this.syncResult is { IsCompleted: false })
			{
				await this.syncResult.NoThrowAwaitable();
			}

			this.client.Dispose();
		}

		private LightWalletClient CreateClient()
		{
			// If this account hasn't had a native wallet created yet, assign a random filename to it.
			if (this.Account.WalletFileName is null)
			{
				this.Account.WalletFileName = $"account.{Path.GetRandomFileName()}.dat";
			}

			return new(
				this.ServerUrl,
				this.Account.ZcashAccount,
				this.owner.confidentialDataPath,
				this.Account.WalletFileName,
				$"{this.Account.WalletFileName}.log",
				watchMemPool: true);
		}
	}
}
