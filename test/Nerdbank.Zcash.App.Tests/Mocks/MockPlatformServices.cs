// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Mocks;

internal class MockPlatformServices : PlatformServices
{
	private bool isOnACPower;

	public override bool IsOnACPower => this.isOnACPower;

	public override bool HasHardwareBackButton => false;

	public override bool IsNetworkMetered => false;

	public override bool IsRunningUnderTest => true;

	public override IDisposable? RequestSleepDeferral() => null;

	internal void SetIsOnACPower(bool value) => this.RaiseAndSetIfChanged(ref this.isOnACPower, value, nameof(this.IsOnACPower));
}
