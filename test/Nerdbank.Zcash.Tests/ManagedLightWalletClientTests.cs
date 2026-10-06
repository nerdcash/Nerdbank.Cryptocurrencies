// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

[Property("RequiresNetwork", "true")]
public class ManagedLightWalletClientTests : TestBase, IDisposable
{
	private ManagedLightWalletClient mainnet = null!; // initialized in InitializeAsync
	private ManagedLightWalletClient testnet = null!; // initialized in InitializeAsync

	[Before(HookType.Test)]
	public async ValueTask InitializeAsync()
	{
		this.mainnet = await ManagedLightWalletClient.CreateAsync(LightWalletServerMainNet, this.TimeoutToken);
		this.testnet = await ManagedLightWalletClient.CreateAsync(LightWalletServerTestNet, this.TimeoutToken);
	}

	public void Dispose()
	{
		this.mainnet?.Dispose();
		this.testnet?.Dispose();
	}

	[Test]
	public void Network()
	{
		Assert.Equal(ZcashNetwork.MainNet, this.mainnet.Network);
		Assert.Equal(ZcashNetwork.TestNet, this.testnet.Network);
	}
}
