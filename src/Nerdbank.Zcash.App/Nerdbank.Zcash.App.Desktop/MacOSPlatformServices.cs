// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash.App.Desktop;

internal class MacOSPlatformServices : PlatformServices
{
	public override bool HasHardwareBackButton => false;

	public override bool IsOnACPower => true;

	public override bool IsNetworkMetered => false;

	public override bool HasAppLevelSystemMenu => true;

	public override IDisposable? RequestSleepDeferral() => null;
}
