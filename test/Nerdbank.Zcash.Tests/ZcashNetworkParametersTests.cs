// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public class ZcashNetworkParametersTests
{
	[Test]
	public void GetParameters()
	{
		Assert.Same(ZcashNetworkParameters.MainNet, ZcashNetworkParameters.GetParameters(ZcashNetwork.MainNet));
		Assert.Same(ZcashNetworkParameters.TestNet, ZcashNetworkParameters.GetParameters(ZcashNetwork.TestNet));
		Assert.Throws<ArgumentOutOfRangeException>(() => ZcashNetworkParameters.GetParameters((ZcashNetwork)int.MaxValue));
	}

	[Test]
	public void MainNet()
	{
		ZcashNetworkParameters parameters = ZcashNetworkParameters.MainNet;
		Assert.Equal(ZcashNetwork.MainNet, parameters.Network);
		Assert.Equal(419_200UL, parameters.SaplingActivationHeight);
	}

	[Test]
	public void TestNet()
	{
		ZcashNetworkParameters parameters = ZcashNetworkParameters.TestNet;
		Assert.Equal(ZcashNetwork.TestNet, parameters.Network);
		Assert.Equal(280_000UL, parameters.SaplingActivationHeight);
	}
}
