// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

[Trait("RequiresNetwork", "true")]
public class LightWalletClientTests : TestBase, IDisposable
{
	private static readonly ZcashAccount DefaultAccount = new(new Zip32HDWallet(Mnemonic, ZcashNetwork.MainNet), 0)
	{
		BirthdayHeight = 2234000,
	};

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
			DefaultAccount,
			this.testDir,
			"zcash-test.wallet",
			"zcash-test.log",
			watchMemPool: false);
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
		ulong birthdayHeight = this.client.BirthdayHeight;
		this.logger.WriteLine($"Birthday height: {birthdayHeight}");
	}

	[Fact]
	public void LastDownloadHeight()
	{
		ulong lastDownloadHeight = this.client.LastDownloadHeight;
		this.logger.WriteLine($"Last sync height: {lastDownloadHeight}");
	}

	[Fact]
	[Trait("Runtime", "Slow")] // The test takes 20+ seconds to run.
	public async Task DownloadTransactionsAsync()
	{
		this.client.UpdateFrequency = TimeSpan.FromMilliseconds(100);
		LightWalletClient.SyncResult result = await this.client.DownloadTransactionsAsync(
			new Progress<LightWalletClient.SyncProgress>(p =>
			{
				this.logger.WriteLine($"Sync progress update: {p}");
			}),
			this.TimeoutToken);
		this.logger.WriteLine($"Sync succeeded: {result.Success}. Scanned {result.TotalBlocksScanned} blocks to reach block {result.LatestBlock}.");
	}

	[Fact]
	public void GetDownloadedTransactions_Empty()
	{
		List<Nerdbank.Zcash.Transaction> transactions = this.client.GetDownloadedTransactions(0);
		Assert.Empty(transactions);
	}

	[Fact]
	public async Task SendAsync_ValidatesNullPayments()
	{
		await Assert.ThrowsAsync<ArgumentNullException>("payments", () => this.client.SendAsync(null!, null, this.TimeoutToken));
	}

	[Fact]
	public async Task SendAsync_EmptySendsList()
	{
		List<Nerdbank.Zcash.Transaction.SendItem> sends = new();
		ArgumentException ex = await Assert.ThrowsAsync<ArgumentException>(() => this.client.SendAsync(sends, null, this.TimeoutToken));
		this.logger.WriteLine(ex.ToString());
	}

	[Fact(Skip = "Skip while ZingoLib panics: https://github.com/zingolabs/zingolib/issues/566")]
	[Trait("Runtime", "Slow")] // The test takes 20+ seconds to run.
	public async Task SendAsync_InsufficientFunds()
	{
		List<Nerdbank.Zcash.Transaction.SendItem> sends = new()
		{
			new Nerdbank.Zcash.Transaction.SendItem(DefaultAccount.DefaultAddress, 1.0m, default),
		};
		LightWalletException ex = await Assert.ThrowsAsync<LightWalletException>(() =>
		this.client.SendAsync(
			sends,
			new Progress<LightWalletClient.SendProgress>(p => this.logger.WriteLine($"{p}")),
			this.TimeoutToken));
		this.logger.WriteLine(ex.Message);
		Assert.Equal(LightWalletException.ErrorCode.Other, ex.Code);
	}
}
