// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash.App.iOS;

#pragma warning disable CS9113 // Parameter is unread.
internal class IosPlatformServices(AppDelegate appDelegate) : PlatformServices
#pragma warning restore CS9113 // Parameter is unread.
{
	public override bool IsOnACPower => false;

	public override bool IsNetworkMetered => true;

	public override IDisposable? RequestSleepDeferral()
	{
		return null;
	}
}
