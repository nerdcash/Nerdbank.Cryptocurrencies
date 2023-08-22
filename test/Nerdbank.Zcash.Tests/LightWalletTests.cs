// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using uniffi.LightWallet;

public class LightWalletTests : TestBase, IDisposable
{
	private static readonly Uri TestLightWalletServer = new("https://zcash.mysideoftheweb.com:9067/");
	private readonly ITestOutputHelper logger;
	private readonly LightWallet wallet;
	private readonly string testDir;

	public LightWalletTests(ITestOutputHelper logger)
	{
		this.logger = logger;

		this.testDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
		Directory.CreateDirectory(this.testDir);
		this.logger.WriteLine($"Test directory: \"{this.testDir}\"");

		this.wallet = new(
			TestLightWalletServer,
			ZcashNetwork.MainNet,
			this.testDir,
			"zcash-test.wallet",
			"zcash-test.log",
			watchMemPool: false);
	}

	public void Dispose()
	{
		this.wallet.Dispose();
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
		ulong height = await this.wallet.GetLatestBlockHeightAsync(this.TimeoutToken);
		this.logger.WriteLine($"Height: {height}");
		Assert.NotEqual(0u, height);
	}

	[Fact]
	public async Task GetLatestBlockHeight_NoServerAtUrl()
	{
		LightWalletException ex = await Assert.ThrowsAnyAsync<LightWalletException>(async () => await LightWallet.GetLatestBlockHeightAsync(new Uri("https://doesnotexist.mysideoftheweb.com/"), this.TimeoutToken));
		this.logger.WriteLine(ex.ToString());
	}

	[Fact]
	public void BirthdayHeight()
	{
		ulong birthdayHeight =  this.wallet.BirthdayHeight;
		this.logger.WriteLine($"Birthday height: {birthdayHeight}");
	}

	[Fact]
	public async Task DownloadTransactionsAsync()
	{
		string result = await this.wallet.DownloadTransactionsAsync(
			new Progress<LightWallet.SyncProgress>(p =>
			{
				this.logger.WriteLine($"Sync progress update: {p}");
			}),
			TimeSpan.FromSeconds(1),
			this.TimeoutToken);
		this.logger.WriteLine(result);
	}
}
