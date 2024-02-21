// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Specialized;
using Microsoft.VisualStudio.Threading;
using IAsyncDisposable = System.IAsyncDisposable;

namespace Nerdbank.Zcash.App;

public class WalletSyncManager : IAsyncDisposable
{
	private readonly string confidentialDataPath;
	private readonly IPlatformServices platformServices;
	private readonly ZcashWallet wallet;
	private readonly AppSettings settings;
	private readonly IContactManager contactManager;
	private readonly ExchangeRateRecord exchangeRateRecord;
	private Dictionary<ZcashNetwork, Tracker> trackers = new();
	private bool syncStarted;

	public WalletSyncManager(string confidentialDataPath, ZcashWallet wallet, AppSettings settings, IContactManager contactManager, ExchangeRateRecord exchangeRateRecord, IPlatformServices platformServices)
	{
		this.confidentialDataPath = confidentialDataPath;
		this.wallet = wallet;
		this.settings = settings;
		this.contactManager = contactManager;
		this.exchangeRateRecord = exchangeRateRecord;
		this.platformServices = platformServices;
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
		foreach (ZcashNetwork network in wallet.Accounts.Select(a => a.Network).Distinct())
		{
			this.trackers.Add(network, new Tracker(this, network));
		}
	}

	private void Wallet_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		if (e.NewItems is not null)
		{
			foreach (Account account in e.NewItems)
			{
				if (!this.trackers.ContainsKey(account.Network))
				{
					this.trackers.Add(account.Network, new Tracker(this, account.Network));
				}
			}
		}
	}

	private class Tracker : IAsyncDisposable
	{
		private readonly WalletSyncManager owner;
		private LightWalletClient client;
		private CancellationTokenSource shutdownTokenSource;
		private Task syncResult;

		public Tracker(WalletSyncManager owner, ZcashNetwork network)
		{
			this.owner = owner;
			this.Network = network;
			this.shutdownTokenSource = new CancellationTokenSource();

			// Initialize the native wallet that will be responsible for syncing this account.
			this.client = this.CreateClient();

			this.InitializeAccounts();

			// Start the process of keeping in sync with new transactions.
			this.syncResult = this.DownloadAsync(this.shutdownTokenSource.Token);
		}

		internal Uri ServerUrl => this.owner.settings.GetLightServerUrl(this.Network);

		internal ZcashNetwork Network { get; }

		private IEnumerable<Account> Accounts => this.owner.wallet.Accounts.Where(a => a.Network == this.Network);

		public async ValueTask DisposeAsync()
		{
			await this.ShutdownWalletAsync();
		}

		private async Task DownloadAsync(CancellationToken cancellationToken)
		{
			bool caughtUp = false;
			Progress<LightWalletClient.SyncProgress> syncProgress = new(v =>
			{
				caughtUp |= v.LastFullyScannedBlock == v.TipHeight;
				foreach (Account account in this.Accounts)
				{
					account.SyncProgress = v;
				}
			});

			Progress<IReadOnlyDictionary<ZcashAccount, IReadOnlyCollection<Transaction>>> discoveredTransactions = new(v =>
			{
				foreach (Account account in this.Accounts)
				{
					if (v.TryGetValue(account.ZcashAccount, out IReadOnlyCollection<Transaction>? transactions))
					{
						account.AddTransactions(transactions, null, this.owner.exchangeRateRecord, this.owner.settings, this.owner.wallet, this.owner.contactManager);
					}
				}
			});

			await this.client.DownloadTransactionsAsync(syncProgress, discoveredTransactions, continually: true, cancellationToken);
		}

		private void InitializeAccounts()
		{
			// TODO: handle re-orgs and rewrite/invalidate the necessary transactions.
			foreach (Account account in this.Accounts)
			{
				account.LightWalletClient = this.client;
				this.client.UpgradeAccount(account.ZcashAccount);

				List<Transaction> txs = this.client.GetDownloadedTransactions(account.ZcashAccount, account.LastBlockHeight);

				account.AddTransactions(txs, this.client.LastDownloadHeight, this.owner.exchangeRateRecord, this.owner.settings, this.owner.wallet, this.owner.contactManager);

				account.Balance = this.client.GetBalances(account.ZcashAccount);

				LightWalletClient.BirthdayHeights birthdayHeights = this.client.GetBirthdayHeights(account.ZcashAccount);
				account.RebirthHeight = birthdayHeights.RebirthHeight;
				account.OptimizedBirthdayHeight = birthdayHeights.BirthdayHeight;
				account.ZcashAccount.BirthdayHeight = birthdayHeights.OriginalBirthdayHeight;
			}
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
			string sqliteDbPath = Path.Combine(this.owner.confidentialDataPath, $"{this.Network}.sqlite");
			return new(this.ServerUrl, this.Network, sqliteDbPath);
		}
	}
}
