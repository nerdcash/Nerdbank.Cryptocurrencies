// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
	private readonly DbInit dbinit;
	private uint accountId; // TODO: figure out what to do for this.
	private ZcashAccount account;

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

	/// <summary>
	/// Gets the raw byte encoding of the unified spending key for this account.
	/// </summary>
	private byte[] UnifiedSpendingKeyBytes
	{
		get
		{
			UnifiedSpendingKey usk = this.account.Spending?.UnifiedKey ?? throw new InvalidOperationException("No spending key.");
			byte[] uskBytes = new byte[500];
			int len = usk.ToBytes(uskBytes);
			return uskBytes[..len];
		}
	}

	/// <summary>
	/// Gets the length of the blockchain (independent of what may have been sync'd thus far.)
	/// </summary>
	/// <param name="lightWalletServerUrl">The URL of the lightwallet server to query.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The length of the blockchain.</returns>
	/// <exception cref="LightWalletException">Thrown if any error occurs.</exception>
	public static ValueTask<uint> GetLatestBlockHeightAsync(Uri lightWalletServerUrl, CancellationToken cancellationToken)
	{
		return new(Task.Run(
			() => LightWalletMethods.LightwalletGetBlockHeight(lightWalletServerUrl.AbsoluteUri),
			cancellationToken));
	}

	public void AddAccount(ZcashAccount account)
	{
		Requires.NotNull(account);

		if (account.Network != this.Network)
		{
			throw new InvalidOperationException(Strings.FormatNetworkMismatch(this.Network, account.Network));
		}

		this.accountId = LightWalletMethods.LightwalletAddAccount(this.dbinit, this.serverUrl.AbsoluteUri, account.HDDerivation.Value.Wallet.Seed.ToArray(), (uint?)account.BirthdayHeight);
		this.account = account;
	}

	/// <summary>
	/// Gets the various forms of birthday heights relevant to this account.
	/// </summary>
	/// <returns>The birthday heights.</returns>
	/// <remarks>
	/// The resulting struct contains fields which may be influenced by the completeness of the sync to the blockchain.
	/// </remarks>
	public BirthdayHeights GetBirthdayHeights() => LightWalletMethods.LightwalletGetBirthdayHeights(this.dbinit, this.accountId);

	/// <inheritdoc cref="GetLatestBlockHeightAsync(Uri, CancellationToken)"/>
	public ValueTask<uint> GetLatestBlockHeightAsync(CancellationToken cancellationToken) => GetLatestBlockHeightAsync(this.serverUrl, cancellationToken);

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
		uniffi.LightWallet.SyncResult result = await Task.Run(
			() => LightWalletMethods.LightwalletSync(this.dbinit, this.serverUrl.AbsoluteUri),
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
		return LightWalletMethods.LightwalletGetTransactions(this.dbinit, this.accountId, startingBlock)
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
	public async Task<TxId> SendAsync(IReadOnlyCollection<SendItem> payments, IProgress<SendProgress>? progress, CancellationToken cancellationToken)
	{
		Requires.NotNullOrEmpty(payments);

		List<TransactionSendDetail> details = [.. payments
			.Select(p => new TransactionSendDetail((ulong)(p.Amount * ZatsPerZEC), p.Memo.RawBytes.ToArray(), p.ToAddress))];

		return await Task.Run(
			delegate
			{
				SendTransactionResult result = LightWalletMethods.LightwalletSend(this.dbinit, this.serverUrl.AbsoluteUri, this.UnifiedSpendingKeyBytes, MinimumConfirmations, details);
				return new TxId(result.txid);
			},
			cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Gets user balances.
	/// </summary>
	/// <returns>Pool balances.</returns>
	public AccountBalances GetBalances() => new(this.Network.AsSecurity(), LightWalletMethods.LightwalletGetUserBalances(this.dbinit, this.accountId, MinimumConfirmations));

	/// <summary>
	/// Discovers all non-zero balances for transparent addresses in this account.
	/// </summary>
	/// <returns>A collection of transparent addresses with positive balances, sorted by the age of the oldest UTXO for each address.</returns>
	/// <remarks>
	/// This can be useful as an input into an algorithm that shields transparent funds.
	/// </remarks>
	public IReadOnlyList<(TransparentAddress Address, decimal Balance)> GetUnshieldedBalances()
	{
		List<TransparentNote> utxos = LightWalletMethods.LightwalletGetUnshieldedUtxos(this.dbinit, this.accountId);
		if (utxos.Count == 0)
		{
			return Array.Empty<(TransparentAddress, decimal)>();
		}

		Dictionary<string, (decimal Balance, int Age)> index = new();
		for (int i = 0; i < utxos.Count; i++)
		{
			TransparentNote utxo = utxos[i];
			if (index.TryGetValue(utxo.recipient, out var existing))
			{
				index[utxo.recipient] = (existing.Balance + ZatsToZEC(utxo.value), existing.Age);
			}
			else
			{
				index[utxo.recipient] = (ZatsToZEC(utxo.value), i);
			}
		}

		return [..
			from account in index
			let addr = (TransparentAddress)ZcashAddress.Decode(account.Key)
			orderby account.Value.Age
			select (addr, account.Value.Balance)];
	}

	/// <summary>
	/// Shields all funds in a given transparent address.
	/// </summary>
	/// <param name="address">The address with UTXOs to be shielded. This value is recommended to have come from a random element in the list returned from <see cref="GetUnshieldedBalances"/>.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>A task that completes when the shielding transaction has been broadcast.</returns>
	public Task ShieldAsync(TransparentAddress address, CancellationToken cancellationToken)
	{
		return Task.Run(
			delegate
			{
				LightWalletMethods.LightwalletShield(this.dbinit, this.serverUrl.AbsoluteUri, this.UnifiedSpendingKeyBytes, address);
			},
			cancellationToken);
	}

	/// <inheritdoc/>
	public void Dispose()
	{
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
		=> new(ZcashAddress.Decode(d.recipient), ZatsToZEC(d.value), new Memo(d.memo.ToArray()));

	/// <summary>
	/// Initializes a new instance of the <see cref="RecvItem"/> class.
	/// </summary>
	/// <param name="d">The uniffi shielded note.</param>
	private static RecvItem CreateRecvItem(ShieldedNote d)
		=> new(ZcashAddress.Decode(d.recipient), ZatsToZEC(d.value), new Memo(d.memo.ToArray()), d.isChange);

	/// <summary>
	/// Initializes a new instance of the <see cref="RecvItem"/> class.
	/// </summary>
	/// <param name="d">The uniffi transparent note.</param>
	private static RecvItem CreateRecvItem(TransparentNote d)
		=> new(ZcashAddress.Decode(d.recipient), ZatsToZEC(d.value), Memo.NoMemo, IsChange: false);

	/// <summary>
	/// Initializes a new instance of the <see cref="Transaction"/> class.
	/// </summary>
	/// <param name="t">The uniffi transaction to copy data from.</param>
	/// <param name="network">The network this transaction is on.</param>
	private static Transaction CreateTransaction(uniffi.LightWallet.Transaction t, ZcashNetwork network)
		=> new(new TxId(t.txid), t.minedHeight, t.expiredUnmined, t.blockTime, ZatsToZEC(t.accountBalanceDelta), ZatsToZEC(t.fee), [.. t.outgoing.Select(CreateSendItem)], [.. t.incomingShielded.Select(CreateRecvItem), .. t.incomingTransparent.Select(CreateRecvItem)]);

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
	}

	/// <summary>
	/// Describes the final result of a blockchain scan.
	/// </summary>
	/// <param name="Success">Indicates overall success of the operation.</param>
	/// <param name="LatestBlock">The last blocked scanned.</param>
	public record SyncResult(
		bool Success,
		ulong LatestBlock)
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="SyncResult"/> class
		/// based on data coming from the native code.
		/// </summary>
		/// <param name="copyFrom">The native data to copy from.</param>
		internal SyncResult(uniffi.LightWallet.SyncResult copyFrom)
			: this(true, copyFrom.latestBlock)
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
	}
}
