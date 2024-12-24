// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

[Trait("RequiresNetwork", "true")]
public class ManagedLightWalletClientTests : TestBase, IAsyncLifetime
{
	private ManagedLightWalletClient mainnet = null!; // initialized in InitializeAsync
	private ManagedLightWalletClient testnet = null!; // initialized in InitializeAsync

	public async ValueTask InitializeAsync()
	{
		this.mainnet = await ManagedLightWalletClient.CreateAsync(LightWalletServerMainNet, this.TimeoutToken);
		this.testnet = await ManagedLightWalletClient.CreateAsync(LightWalletServerTestNet, this.TimeoutToken);
	}

	public ValueTask DisposeAsync()
	{
		this.mainnet.Dispose();
		this.testnet.Dispose();
		return ValueTask.CompletedTask;
	}

	[Fact]
	public void Network()
	{
		Assert.Equal(ZcashNetwork.MainNet, this.mainnet.Network);
		Assert.Equal(ZcashNetwork.TestNet, this.testnet.Network);
	}
}
