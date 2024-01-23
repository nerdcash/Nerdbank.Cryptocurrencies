// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public class ZcashNetworkParametersTests
{
	[Fact]
	public void GetParameters()
	{
		Assert.Same(ZcashNetworkParameters.MainNet, ZcashNetworkParameters.GetParameters(ZcashNetwork.MainNet));
		Assert.Same(ZcashNetworkParameters.TestNet, ZcashNetworkParameters.GetParameters(ZcashNetwork.TestNet));
		Assert.Throws<ArgumentOutOfRangeException>(() => ZcashNetworkParameters.GetParameters((ZcashNetwork)int.MaxValue));
	}

	[Fact]
	public void MainNet()
	{
		ZcashNetworkParameters parameters = ZcashNetworkParameters.MainNet;
		Assert.Equal(ZcashNetwork.MainNet, parameters.Network);
		Assert.Equal(419_200UL, parameters.SaplingActivationHeight);
	}

	[Fact]
	public void TestNet()
	{
		ZcashNetworkParameters parameters = ZcashNetworkParameters.TestNet;
		Assert.Equal(ZcashNetwork.TestNet, parameters.Network);
		Assert.Equal(280_000UL, parameters.SaplingActivationHeight);
	}
}
