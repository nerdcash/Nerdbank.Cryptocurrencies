// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

[Trait("RequiresNetwork", "true")]
public class LightWalletClientTests : TestBase, IDisposable, IAsyncLifetime
{
	private static readonly ZcashAccount DefaultAccount = new(new Zip32HDWallet(Mnemonic, ZcashNetwork.MainNet), 0);
	private static bool defaultAccountBirthdayHeightSet;

	private readonly ITestOutputHelper logger;
	private readonly LightWalletClient client;
	private readonly string testDir;

	public LightWalletClientTests(ITestOutputHelper logger)
	{
		this.logger = logger;

		this.testDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
		Directory.CreateDirectory(this.testDir);
		this.logger.WriteLine($"Test directory: \"{this.testDir}\"");

		this.client = new(
			LightWalletServerMainNet,
			DefaultAccount.Network,
			Path.Join(this.testDir, "zcash-test.wallet"));
	}

	public async ValueTask InitializeAsync()
	{
		if (!defaultAccountBirthdayHeightSet)
		{
			DefaultAccount.BirthdayHeight = await this.client.GetLatestBlockHeightAsync(this.TimeoutToken) - 5;
			defaultAccountBirthdayHeightSet = true;
		}

		await this.client.AddAccountAsync(DefaultAccount, this.TimeoutToken);
	}

	public ValueTask DisposeAsync()
	{
		return ValueTask.CompletedTask;
	}

	public void Dispose()
	{
		this.client.Dispose();
		Directory.Delete(this.testDir, recursive: true);
	}

	[Fact]
	public void Deinitialize()
	{
		// This test intentionally left blank. Its body is in the Dispose method of this class.
	}

	[Fact]
	public async Task GetLatestBlockHeight()
	{
		ulong height = await this.client.GetLatestBlockHeightAsync(this.TimeoutToken);
		this.logger.WriteLine($"Height: {height}");
		Assert.NotEqual(0u, height);
	}

	[Fact]
	public async Task GetLatestBlockHeight_Static()
	{
		ulong height = await LightWalletClient.GetLatestBlockHeightAsync(LightWalletServerMainNet, this.TimeoutToken);
		this.logger.WriteLine($"Height: {height}");
		Assert.NotEqual(0u, height);
	}

	[Fact]
	public async Task GetLatestBlockHeight_NoServerAtUrl()
	{
		LightWalletException ex = await Assert.ThrowsAnyAsync<LightWalletException>(async () => await LightWalletClient.GetLatestBlockHeightAsync(new Uri("https://doesnotexist.mysideoftheweb.com/"), this.TimeoutToken));
		this.logger.WriteLine(ex.ToString());
	}

	[Fact]
	public void BirthdayHeight()
	{
		uint? birthdayHeight = this.client.BirthdayHeight;
		this.logger.WriteLine($"Birthday height: {birthdayHeight}");
	}

	[Fact]
	public void LastDownloadHeight()
	{
		uint? lastDownloadHeight = this.client.LastDownloadHeight;
		this.logger.WriteLine($"Last sync height: {lastDownloadHeight}");
	}

	[Fact]
	public async Task DownloadTransactionsAsync()
	{
		LightWalletClient.SyncProgress result = await this.client.DownloadTransactionsAsync(
			new Progress<LightWalletClient.SyncProgress>(p =>
			{
				this.logger.WriteLine($"Sync progress update: {p}");
			}),
			null,
			continually: false,
			this.TimeoutToken);
		this.logger.WriteLine($"Sync succeeded. Scanned to block {result.LastFullyScannedBlock}.");
	}

	[Fact]
	public void GetDownloadedTransactions_Empty()
	{
		List<Nerdbank.Zcash.Transaction> transactions = this.client.GetDownloadedTransactions(DefaultAccount, 0);
		Assert.Empty(transactions);
	}

	[Fact]
	public void GetIncomingPayments_Empty()
	{
		ZcashAddress address = DefaultAccount.DefaultAddress;
		List<Nerdbank.Zcash.Transaction> transactions = this.client.GetIncomingPayments(address, 0);
		Assert.Empty(transactions);
	}

	[Fact]
	public async Task SendAsync_ValidatesNullArgs()
	{
		await Assert.ThrowsAsync<ArgumentNullException>("account", () => this.client.SendAsync(null!, Array.Empty<Transaction.LineItem>(), null, this.TimeoutToken));
		await Assert.ThrowsAsync<ArgumentNullException>("payments", () => this.client.SendAsync(DefaultAccount, null!, null, this.TimeoutToken));
	}

	[Fact]
	public async Task SendAsync_EmptySendsList()
	{
		List<Nerdbank.Zcash.Transaction.LineItem> sends = new();
		ArgumentException ex = await Assert.ThrowsAsync<ArgumentException>(() => this.client.SendAsync(DefaultAccount, sends, null, this.TimeoutToken));
		this.logger.WriteLine(ex.ToString());
	}

	[Fact]
	public async Task SendAsync_InsufficientFunds()
	{
		List<Nerdbank.Zcash.Transaction.LineItem> sends = new()
		{
			new Nerdbank.Zcash.Transaction.LineItem(DefaultAccount.DefaultAddress, 1.0m, default),
		};
		LightWalletException ex = await Assert.ThrowsAsync<LightWalletException>(() =>
			this.client.SendAsync(
				DefaultAccount,
				sends,
				new Progress<LightWalletClient.SendProgress>(p => this.logger.WriteLine($"{p}")),
				this.TimeoutToken));
		this.logger.WriteLine(ex.Message);
		Assert.Equal(LightWalletException.ErrorCode.Other, ex.Code);
	}

	/// <summary>
	/// Verifies that diversifier index collisions are handled gracefully.
	/// In particular, handled by just reporting success.
	/// </summary>
	[Fact]
	public void AddDiversifier_IndexCollision()
	{
		// Use the index of the default address to ensure a collision.
		// Given the seed hard-coded for the tests, this is expected to be '3'.
		////Assert.True(DefaultAccount.TryGetDiversifierIndex(DefaultAccount.DefaultAddress, out DiversifierIndex? diversifierIndex));
		UnifiedAddress ua = this.client.AddDiversifier(DefaultAccount, 3/*diversifierIndex.Value*/);
		Assert.NotNull(ua.GetPoolReceiver<OrchardReceiver>());
		Assert.NotNull(ua.GetPoolReceiver<TransparentP2PKHReceiver>());
		Assert.NotNull(ua.GetPoolReceiver<SaplingReceiver>());
	}

	[Fact]
	public void AddDiversifier_InvalidSapling()
	{
		UnifiedAddress ua = this.client.AddDiversifier(DefaultAccount, new DiversifierIndex(500));
		Assert.NotNull(ua.GetPoolReceiver<OrchardReceiver>());
		Assert.NotNull(ua.GetPoolReceiver<TransparentP2PKHReceiver>());

		// It so happens that this seed and index produces an invalid sapling receiver.
		Assert.Null(ua.GetPoolReceiver<SaplingReceiver>());
	}

	[Fact]
	public void AddDiversifier_InvalidTransparent()
	{
		// Use an index that is outside the range 32-bit range supported by transparent addresses.
		UnifiedAddress ua = this.client.AddDiversifier(DefaultAccount, new DiversifierIndex((ulong)uint.MaxValue + 1));
		Assert.NotNull(ua.GetPoolReceiver<OrchardReceiver>());
		Assert.Null(ua.GetPoolReceiver<TransparentP2PKHReceiver>());

		// It so happens that this index produces a valid sapling receiver.
		Assert.NotNull(ua.GetPoolReceiver<SaplingReceiver>());
	}
}
