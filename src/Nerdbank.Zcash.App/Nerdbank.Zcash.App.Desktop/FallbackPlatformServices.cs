// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using ReactiveUI;

namespace Nerdbank.Zcash.App.Desktop;

internal class FallbackPlatformServices : ReactiveObject, IPlatformServices
{
	public bool HasHardwareBackButton => false;

	public bool IsOnACPower => true;

	public bool IsNetworkMetered => false;

	public IDisposable? RequestSleepDeferral() => null;
}
