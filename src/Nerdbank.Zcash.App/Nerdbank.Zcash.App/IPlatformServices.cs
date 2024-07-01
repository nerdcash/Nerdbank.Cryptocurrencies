// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;

namespace Nerdbank.Zcash.App;

public interface IPlatformServices : INotifyPropertyChanged
{
	/// <summary>
	/// Gets a value indicating whether the platform provides a UI "back" button,
	/// such that the app's own UI need not display one.
	/// </summary>
	bool HasHardwareBackButton { get; }

	/// <summary>
	/// Gets a value indicating whether the system is running on AC power.
	/// </summary>
	bool IsOnACPower { get; }

	/// <summary>
	/// Gets a value indicating whether the current network connection is metered.
	/// </summary>
	bool IsNetworkMetered { get; }

	/// <summary>
	/// Requests that the system not sleep until the returned object is disposed.
	/// </summary>
	/// <returns>
	/// A value to dispose of when the need to defer sleep is over.
	/// It <em>may</em> be <see langword="null"/> when the request will not be filled.
	/// But it may be non-<see langword="null"/> even when the request is not filled.
	/// </returns>
	/// <remarks>
	/// This method does not guarantee that the system will not sleep. It merely requests that it not sleep.
	/// For example, it will only be effective when on AC power.
	/// </remarks>
	public IDisposable? RequestSleepDeferral();
}
