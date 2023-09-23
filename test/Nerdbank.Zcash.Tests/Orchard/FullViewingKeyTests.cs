// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Zcash.Orchard;

namespace Orchard;

public class FullViewingKeyTests : TestBase
{
	private readonly ITestOutputHelper logger;

	public FullViewingKeyTests(ITestOutputHelper logger)
	{
		this.logger = logger;
	}

	[Fact]
	public void DeriveInternal()
	{
		FullViewingKey fvk = new Zip32HDWallet(Mnemonic, ZcashNetwork.MainNet).CreateOrchardAccount().FullViewingKey;
		this.logger.WriteLine($"Public address: {fvk.IncomingViewingKey.DefaultAddress}");
		FullViewingKey internalFvk = fvk.DeriveInternal();
		this.logger.WriteLine($"Internal address: {internalFvk.IncomingViewingKey.DefaultAddress}");
		Assert.Equal("u1pxm93mp9jct7h5u7rref63mht9eqcskhsw86q3dntj7pt8w3jqj6vscsx09fz4z5lc277t4vpcexc46vus7twg3n8szv0cnucuzneuhh", internalFvk.IncomingViewingKey.DefaultAddress);
	}
}
