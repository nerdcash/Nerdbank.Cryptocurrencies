// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;

namespace Nerdbank.Zcash.App.iOS;

#pragma warning disable CS9113 // Parameter is unread.
internal class IosPlatformServices : PlatformServices
#pragma warning restore CS9113 // Parameter is unread.
{
	private readonly ILoggerFactory loggerFactory;

	public IosPlatformServices(AppDelegate appDelegate)
	{
		this.loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(this.ConfigureLogging);
	}

	public override bool IsOnACPower => false;

	public override bool IsNetworkMetered => true;

	public override ILoggerFactory LoggerFactory => this.loggerFactory;

	public override IDisposable? RequestSleepDeferral()
	{
		return null;
	}
}
