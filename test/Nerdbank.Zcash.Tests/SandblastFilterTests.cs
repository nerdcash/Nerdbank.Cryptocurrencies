// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public class SandblastFilterTests
{
	/// <summary>
	/// The opt-in flag is interpreted by the native scanner, and the default (off) does not skip.
	/// </summary>
	[Test]
	public void SkipSandblastTrialDecryption_ReachesNativeLayer_AndDefaultsOff()
	{
		const uint insideSandblastRange = 1_820_000;
		const ulong manyShieldedOutputs = 51;

		Assert.False(LightWalletClient.SkipsSandblastTrialDecryption(false, ZcashNetwork.MainNet, insideSandblastRange, manyShieldedOutputs));

		Assert.True(LightWalletClient.SkipsSandblastTrialDecryption(true, ZcashNetwork.MainNet, insideSandblastRange, manyShieldedOutputs));
		Assert.False(LightWalletClient.SkipsSandblastTrialDecryption(true, ZcashNetwork.MainNet, insideSandblastRange, 50));
		Assert.False(LightWalletClient.SkipsSandblastTrialDecryption(true, ZcashNetwork.MainNet, 1_709_999, manyShieldedOutputs));
		Assert.True(LightWalletClient.SkipsSandblastTrialDecryption(true, ZcashNetwork.MainNet, 1_710_000, manyShieldedOutputs));
		Assert.True(LightWalletClient.SkipsSandblastTrialDecryption(true, ZcashNetwork.MainNet, 2_050_000, manyShieldedOutputs));
		Assert.False(LightWalletClient.SkipsSandblastTrialDecryption(true, ZcashNetwork.MainNet, 2_050_001, manyShieldedOutputs));
		Assert.False(LightWalletClient.SkipsSandblastTrialDecryption(true, ZcashNetwork.TestNet, insideSandblastRange, 10_000));
	}
}
