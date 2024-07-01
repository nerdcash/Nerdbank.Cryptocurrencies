// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using ReactiveUI;

namespace Mocks;

internal class MockPlatformServices : ReactiveObject, IPlatformServices
{
	private bool isOnACPower;

	public bool IsOnACPower
	{
		get => this.isOnACPower;
		set => this.RaiseAndSetIfChanged(ref this.isOnACPower, value);
	}

	public bool HasHardwareBackButton => false;

	public bool IsNetworkMetered => false;

	public IDisposable? RequestSleepDeferral()
	{
		return null;
	}
}
