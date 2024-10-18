// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if WINDOWS

using System.Reactive.Disposables;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Nerdbank.Zcash.App.Desktop.ViewModels;
using Nerdbank.Zcash.App.ViewModels;
using Windows.Networking.Connectivity;
using Windows.Win32;
using Windows.Win32.System.Power;

namespace Nerdbank.Zcash.App.Desktop;

internal class WindowsPlatformServices : PlatformServices
{
	private readonly Thread powerManagementThread;
	private readonly object powerManagementThreadLock = new();
	private readonly ILoggerFactory loggerFactory;
	private int keepAwakeCounter;
	private bool isOnACPower;
	private bool isNetworkMetered;

	internal WindowsPlatformServices()
	{
		this.loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(this.ConfigureLogging);

		NetworkInformation.NetworkStatusChanged += this.NetworkInformation_NetworkStatusChanged;

		SystemEvents.PowerModeChanged += this.SystemEvents_PowerModeChanged;
		this.UpdateIsOnACPower();
		this.UpdateIsNetworkMetered();

		this.powerManagementThread = new Thread(s => ((WindowsPlatformServices)s!).PowerManagementThreadProc(), 100 * 1024)
		{
			// We don't want this thread to block the process from exiting.
			IsBackground = true,
		};
		this.powerManagementThread.Start(this);
	}

	public override bool IsOnACPower => this.isOnACPower;

	public override bool IsNetworkMetered => this.isNetworkMetered;

	public override ILoggerFactory LoggerFactory => this.loggerFactory;

	public override IDisposable? RequestSleepDeferral()
	{
		// It's important that we do NOT insist that the device stay running on battery power,
		// both because we don't want to drain the battery, but because the user may have a laptop
		// and closed the lid, and slid it in their backpack, and we don't want to overheat the device.
		// But it turns out Windows (11) seems to put the device to sleep in these conditions anyway.
		// So we only need to keep some thread active and requesting that the device stay awake when
		// a client wants to.
		lock (this.powerManagementThreadLock)
		{
			if (this.keepAwakeCounter++ == 0)
			{
				// We transitioned from 0 to 1, so wake the power management thread so it can apply the change.
				Monitor.Pulse(this.powerManagementThreadLock);
			}
		}

		return Disposable.Create(delegate
		{
			lock (this.powerManagementThreadLock)
			{
				if (--this.keepAwakeCounter == 0)
				{
					// We transitioned from 1 to 0, so wake the power management thread so it can apply the change.
					Monitor.Pulse(this.powerManagementThreadLock);
				}
			}
		});
	}

	public override CameraViewModel? CreateCameraViewModel()
		=> this.ViewModelServices is not null ? new WindowsCameraViewModel(this.ViewModelServices) : null;

	private void SystemEvents_PowerModeChanged(object sender, PowerModeChangedEventArgs e)
	{
		if (e.Mode == PowerModes.StatusChange)
		{
			this.UpdateIsOnACPower();
		}
	}

	private void NetworkInformation_NetworkStatusChanged(object sender)
	{
		this.UpdateIsNetworkMetered();
	}

	private void SetIsOnACPower(bool value) => this.RaiseAndSetIfChanged(ref this.isOnACPower, value, nameof(this.IsOnACPower));

	private void SetIsNetworkMetered(bool value) => this.RaiseAndSetIfChanged(ref this.isNetworkMetered, value, nameof(this.IsNetworkMetered));

	private void UpdateIsOnACPower() => this.SetIsOnACPower(SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Online);

	private void UpdateIsNetworkMetered() => this.SetIsNetworkMetered(NetworkInformation.GetInternetConnectionProfile().GetConnectionCost().NetworkCostType == NetworkCostType.Variable);

	private void PowerManagementThreadProc()
	{
		lock (this.powerManagementThreadLock)
		{
			while (true)
			{
				EXECUTION_STATE state = this.keepAwakeCounter > 0
					? EXECUTION_STATE.ES_CONTINUOUS | EXECUTION_STATE.ES_SYSTEM_REQUIRED
					: EXECUTION_STATE.ES_CONTINUOUS;
				PInvoke.SetThreadExecutionState(state);
				Monitor.Wait(this.powerManagementThreadLock);
			}
		}
	}
}

#endif
