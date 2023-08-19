// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public class LightWalletTests : TestBase, IDisposable
{
	private static readonly Uri TestLightWalletServer = new("https://zcash.mysideoftheweb.com:9067/");
	private readonly ITestOutputHelper logger;
	private readonly LightWallet wallet = new(TestLightWalletServer, ZcashNetwork.MainNet);

	public LightWalletTests(ITestOutputHelper logger)
	{
		this.logger = logger;
	}

	public void Dispose()
	{
		this.wallet.Dispose();
	}

	[Fact]
	public void Deinitialize()
	{
		// This test intentionally left blank. Its body is in the Dispose method of this class.
	}

	[Fact]
	public async Task GetLatestBlockHeight()
	{
		ulong height = await this.wallet.GetLatestBlockHeightAsync(CancellationToken.None);
		this.logger.WriteLine($"Height: {height}");
		Assert.NotEqual(0u, height);
	}
}
