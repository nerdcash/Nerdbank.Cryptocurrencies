// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Android.Content;
using Android.Net;
using Android.OS;
using Microsoft;
using ReactiveUI;

namespace Nerdbank.Zcash.App.Android;

internal class AndroidPlatformServices : ReactiveObject, IPlatformServices
{
	private readonly Context context;
	private readonly PowerManager? powerManager;
	private bool isOnACPower;
	private bool isNetworkMetered = true;

	internal AndroidPlatformServices(Context context)
	{
		this.context = context;
		this.powerManager = (PowerManager?)context.GetSystemService(Context.PowerService);

		Receiver receiver = new(this);

		// Manage IsOnACPower property now and as it changes.
		context.RegisterReceiver(receiver, new IntentFilter(Intent.ActionPowerConnected));
		this.isOnACPower = IsConnected(context);

		// Manage IsNetworkMetered property now and as it changes.
		var connectivityManager = (ConnectivityManager?)context.GetSystemService(Context.ConnectivityService);
		Assumes.Present(connectivityManager);
		NetworkRequest networkRequest = new NetworkRequest.Builder()
			.AddCapability(NetCapability.Internet)?
			.AddTransportType(TransportType.Wifi)?
			.AddTransportType(TransportType.Ethernet)?
			.AddTransportType(TransportType.Cellular)?
			.Build() ?? throw new Exception("Unable to build network request");
		connectivityManager.RegisterNetworkCallback(networkRequest, new NetworkSink(this));
	}

	public bool HasHardwareBackButton => true;

	public bool IsOnACPower
	{
		get => this.isOnACPower;
		private set => this.RaiseAndSetIfChanged(ref this.isOnACPower, value);
	}

	public bool IsNetworkMetered
	{
		get => this.isNetworkMetered;
		private set => this.RaiseAndSetIfChanged(ref this.isNetworkMetered, value);
	}

	public IDisposable? RequestSleepDeferral()
	{
		if (this.powerManager is null)
		{
			return null;
		}

		PowerManager.WakeLock? wakeLock = this.powerManager.NewWakeLock(WakeLockFlags.Partial, nameof(AndroidPlatformServices));
		if (wakeLock is null)
		{
			return null;
		}

		wakeLock.Acquire();
		return new DisposableAction(() =>
		{
			if (wakeLock.IsHeld)
			{
				wakeLock.Release();
			}

			wakeLock.Dispose();
		});
	}

	private static bool IsConnected(Context context)
	{
		Intent? intent = context.RegisterReceiver(null, new IntentFilter(Intent.ActionBatteryChanged));
		Assumes.NotNull(intent);
		int plugged = intent.GetIntExtra(BatteryManager.ExtraPlugged, -1);
		return plugged == (int)BatteryPlugged.Ac || plugged == (int)BatteryPlugged.Usb;
	}

	private class Receiver(AndroidPlatformServices platformServices) : BroadcastReceiver
	{
		public override void OnReceive(Context? context, Intent? intent)
		{
			switch (intent?.Action)
			{
				case Intent.ActionPowerConnected:
					platformServices.IsOnACPower = true;
					break;
				case Intent.ActionPowerDisconnected:
					platformServices.IsOnACPower = false;
					break;
				default:
					break;
			}
		}
	}

	private class NetworkSink(AndroidPlatformServices platformServices) : ConnectivityManager.NetworkCallback
	{
		public override void OnAvailable(Network network)
		{
		}

		public override void OnUnavailable()
		{
		}

		public override void OnLost(Network network)
		{
		}

		public override void OnCapabilitiesChanged(Network network, NetworkCapabilities networkCapabilities)
		{
			platformServices.IsNetworkMetered = !networkCapabilities.HasCapability(NetCapability.NotMetered);
		}
	}
}
