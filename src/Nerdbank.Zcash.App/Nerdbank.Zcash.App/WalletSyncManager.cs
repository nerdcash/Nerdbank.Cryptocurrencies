// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Microsoft.VisualStudio.Threading;
using IAsyncDisposable = System.IAsyncDisposable;

namespace Nerdbank.Zcash.App;

public class WalletSyncManager : ReactiveObject, IAsyncDisposable
{
	private readonly string confidentialDataPath;
	private readonly IPlatformServices platformServices;
	private readonly ZcashWallet wallet;
	private readonly AppSettings settings;
	private readonly IContactManager contactManager;
	private readonly ExchangeRateRecord exchangeRateRecord;
	private readonly JoinableTaskCollection backgroundTasks;
	private readonly JoinableTaskFactory joinableTaskFactory;
	private readonly CancellationTokenSource shutdownTokenSource = new();
	private readonly Dictionary<ZcashNetwork, Tracker> trackers = new();
	private readonly ObservableCollection<SyncProgressData> syncProgressDatas = new();
	private IViewModelServices? viewModelServices;
	private SyncProgressData? blendedSyncProgress;

	public WalletSyncManager(
		JoinableTaskContext joinableTaskContext,
		string confidentialDataPath,
		ZcashWallet wallet,
		AppSettings settings,
		IContactManager contactManager,
		ExchangeRateRecord exchangeRateRecord,
		IPlatformServices platformServices)
	{
		this.backgroundTasks = joinableTaskContext.CreateCollection();
		this.joinableTaskFactory = joinableTaskContext.CreateFactory(this.backgroundTasks);

		this.confidentialDataPath = confidentialDataPath;
		this.wallet = wallet;
		this.settings = settings;
		this.contactManager = contactManager;
		this.exchangeRateRecord = exchangeRateRecord;
		this.platformServices = platformServices;

		this.ProgressDetails = new(this.syncProgressDatas);
	}

	public interface ITracker
	{
		ZcashNetwork Network { get; }

		void Retry();
	}

	public SyncProgressData? BlendedSyncProgress
	{
		get => this.blendedSyncProgress;
		private set => this.RaiseAndSetIfChanged(ref this.blendedSyncProgress, value);
	}

	public ReadOnlyObservableCollection<SyncProgressData> ProgressDetails { get; }

	public async ValueTask DisposeAsync()
	{
		await this.shutdownTokenSource.CancelAsync();
		INotifyCollectionChanged accounts = this.wallet.Accounts;
		accounts.CollectionChanged -= this.Wallet_CollectionChanged;
		await this.backgroundTasks.JoinTillEmptyAsync();

		// Wait for trackers to conclude their work.
		await Task.WhenAll(this.trackers.Values.Select(t => t.DisposeAsync().AsTask()));
	}

	public void StartSyncing(IViewModelServices viewModelServices, ZcashWallet wallet)
	{
		Verify.Operation(this.viewModelServices is null, "Syncing has already started.");
		this.viewModelServices = viewModelServices;

		INotifyCollectionChanged accounts = wallet.Accounts;
		accounts.CollectionChanged += this.Wallet_CollectionChanged;
		ImmutableArray<SyncProgressData>.Builder datas = ImmutableArray.CreateBuilder<SyncProgressData>(2);
		foreach (ZcashNetwork network in wallet.Accounts.Select(a => a.Network).Distinct())
		{
			Tracker tracker = new(this, network);
			this.trackers.Add(network, tracker);
			this.syncProgressDatas.Add(tracker.SyncProgress);
			datas.Add(tracker.SyncProgress);
		}

		this.BlendedSyncProgress = SyncProgressData.Blend(viewModelServices, datas.ToImmutable());
	}

	public SyncProgressData? GetSyncProgress(ZcashNetwork network)
		=> this.trackers.TryGetValue(network, out Tracker? tracker) ? tracker.SyncProgress : null;

	private void Wallet_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		if (e.NewItems is not null)
		{
			foreach (Account account in e.NewItems)
			{
				if (this.trackers.TryGetValue(account.Network, out Tracker? tracker))
				{
					_ = this.joinableTaskFactory.RunAsync(() => tracker.AddAccountAsync(account.ZcashAccount));
				}
				else
				{
					tracker = new(this, account.Network);
					this.trackers.Add(account.Network, tracker);
					this.syncProgressDatas.Add(tracker.SyncProgress);
					(this.BlendedSyncProgress as IDisposable)?.Dispose();
					this.BlendedSyncProgress = SyncProgressData.Blend(
						this.viewModelServices ?? throw new InvalidOperationException(),
						this.trackers.Values.Select(t => t.SyncProgress).ToImmutableArray());
				}
			}
		}
	}

	private class Tracker : ITracker, IAsyncDisposable
	{
		private static readonly TimeSpan MinimumAutoRetryDelay = TimeSpan.FromSeconds(5);
		private readonly WalletSyncManager owner;
		private readonly LightWalletClient client;
		private readonly JoinableTask completion;
		private readonly AsyncAutoResetEvent unblockAutoShielding = new();
		private readonly AsyncAutoResetEvent retryOnFailure = new(allowInliningAwaiters: false);

		public Tracker(WalletSyncManager owner, ZcashNetwork network)
		{
			this.owner = owner;
			this.Network = network;

			// Initialize the native wallet that will be responsible for syncing this account.
			this.client = this.CreateClient();

			this.completion = owner.joinableTaskFactory.RunAsync(async delegate
			{
				await this.InitializeAccountsAsync(this.owner.shutdownTokenSource.Token);

				// Start the process of keeping in sync with new transactions
				// and activate auto-shielding.
				await Task.WhenAll(
					this.DownloadAsync(this.owner.shutdownTokenSource.Token),
					this.AutoShieldAsync(this.owner.shutdownTokenSource.Token));
			});

			this.SyncProgress = new SyncProgressData(owner.viewModelServices ?? throw new InvalidOperationException(), this);
		}

		public ZcashNetwork Network { get; }

		internal SyncProgressData SyncProgress { get; }

		internal Uri ServerUrl => this.owner.settings.GetLightServerUrl(this.Network);

		private ImmutableArray<Account> Accounts => this.owner.wallet.Accounts.Where(a => a.Network == this.Network).ToImmutableArray();

		public void Retry() => this.retryOnFailure.Set();

		public async ValueTask DisposeAsync()
		{
			await this.ShutdownWalletAsync();
		}

		internal Task AddAccountAsync(ZcashAccount account)
		{
			return this.client.AddAccountAsync(account, this.owner.shutdownTokenSource.Token);
		}

		private async Task DownloadAsync(CancellationToken cancellationToken)
		{
			TimeSpan autoRetryTimeout = MinimumAutoRetryDelay;
			while (true)
			{
				IDisposable? sleepDeferral = this.owner.platformServices.RequestSleepDeferral();
				try
				{
					Progress<LightWalletClient.SyncProgress> syncProgress = new(v =>
					{
						if (string.IsNullOrEmpty(v.LastError))
						{
							// Now that there's an indication of a successful connection,
							// reset our exponential back-off, which may have been increased earlier.
							autoRetryTimeout = MinimumAutoRetryDelay;
						}

						if (v.LastFullyScannedBlock == v.TipHeight)
						{
							// Now that we've caught up, we no longer need to keep the system awake.
							sleepDeferral?.Dispose();
							sleepDeferral = null;

							foreach (Account account in this.Accounts)
							{
								// Only update the last block height if we actually found transactions.
								// Otherwise this could be a recently imported account whose scan hasn't even started.
								// This property is only meant to optimize not re-retrieving transactions from across the interop anyway.
								if (account.Transactions.Count > 0)
								{
									account.LastBlockHeight = v.TipHeight;
								}
							}

							this.unblockAutoShielding.Set();
						}

						this.SyncProgress.Apply(v);

						foreach (Account account in this.Accounts)
						{
							account.Balance = this.client.GetBalances(account.ZcashAccount);
						}
					});

					Progress<IReadOnlyDictionary<ZcashAccount, IReadOnlyCollection<Transaction>>> discoveredTransactions = new(v =>
					{
						foreach (Account account in this.Accounts)
						{
							if (v.TryGetValue(account.ZcashAccount, out IReadOnlyCollection<Transaction>? transactions))
							{
								// Filter transactions to those mined at or above the account's birthday height
								// to workaround https://github.com/zcash/librustzcash/issues/1436.
								IEnumerable<Transaction> filteredTransactions = transactions.Where(t => t.MinedHeight is null || t.MinedHeight >= account.ZcashAccount.BirthdayHeight);
								account.AddTransactions(filteredTransactions, null, this.owner.exchangeRateRecord, this.owner.settings, this.owner.wallet, this.owner.contactManager);
							}

							account.Balance = this.client.GetBalances(account.ZcashAccount);
						}
					});

					await this.client.DownloadTransactionsAsync(syncProgress, discoveredTransactions, continually: true, cancellationToken);
				}
				catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
				{
					this.SyncProgress.ReportDisconnect(ex.Message);

					// Don't keep the device awake while we wait for the next sync attempt.
					sleepDeferral?.Dispose();

					// Wait for the user to retry, or according to a exponential backoff policy.
					using CancellationTokenSource userRetryWaiterCancellation = new();
					await Task.WhenAny(this.retryOnFailure.WaitAsync(userRetryWaiterCancellation.Token), Task.Delay(autoRetryTimeout, cancellationToken));
					await userRetryWaiterCancellation.CancelAsync(); // Don't consume the next signal from the user. We're not waiting any more.
					autoRetryTimeout *= 2; // Wait twice as long next time (but a successful connection will reset this to the minimum).
				}
				finally
				{
					sleepDeferral?.Dispose();
				}
			}
		}

		private async Task AutoShieldAsync(CancellationToken cancellationToken)
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				// Wait for new transactions to arrive, or at least that sync has reached the tip of the blockchain.
				await this.unblockAutoShielding.WaitAsync(cancellationToken);

				foreach (Account account in this.Accounts)
				{
					IReadOnlyList<(TransparentAddress Address, decimal Balance)> unshieldedBalances = this.client.GetUnshieldedBalances(account.ZcashAccount);
					if (unshieldedBalances.Count > 0)
					{
						(TransparentAddress address, decimal _) = unshieldedBalances[Random.Shared.Next(unshieldedBalances.Count)];
						await this.client.ShieldAsync(account.ZcashAccount, address, cancellationToken);

						// Now that we've shielded an address, wait a random time before shielding other addresses
						// to avoid providing evidence on the blockchain that multiple transparent addresses are
						// owned by the same entity.
						await Task.Delay(TimeSpan.FromSeconds(Random.Shared.Next(90, 15 * 60)), cancellationToken);
					}
				}
			}
		}

		private async Task InitializeAccountsAsync(CancellationToken cancellationToken)
		{
			// TODO: Handle re-orgs and rewrite/invalidate the necessary transactions.
			//       Probably by considering LastBlockHeight to never be closer than 100 blocks from the tip.
			//       And then culling the transactions that are no longer valid.
			foreach (Account account in this.Accounts)
			{
				account.LightWalletClient = this.client;
				await this.client.AddAccountAsync(account.ZcashAccount, cancellationToken);
				for (uint addressIndex = 0; addressIndex <= account.ZcashAccount.MaxTransparentAddressIndex; addressIndex++)
				{
					this.client.AddDiversifier(account.ZcashAccount, addressIndex);
				}

				List<Transaction> txs = this.client.GetDownloadedTransactions(account.ZcashAccount, account.LastBlockHeight + 1);

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
			try
			{
				await this.completion;
			}
			catch
			{
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
