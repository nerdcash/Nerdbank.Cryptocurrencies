// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;

namespace Nerdbank.Zcash.App.Desktop;

internal class MacOSPlatformServices : PlatformServices
{
	private readonly ILoggerFactory loggerFactory;

	public MacOSPlatformServices()
	{
		this.loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(this.ConfigureLogging);
	}

	public override bool HasHardwareBackButton => false;

	public override bool IsOnACPower => true;

	public override bool IsNetworkMetered => false;

	public override bool HasAppLevelSystemMenu => true;

	public override ILoggerFactory LoggerFactory => this.loggerFactory;

	public override IDisposable? RequestSleepDeferral() => null;
}
