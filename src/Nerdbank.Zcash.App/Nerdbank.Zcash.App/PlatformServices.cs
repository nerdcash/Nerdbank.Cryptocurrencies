// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Nerdbank.Zcash.App;

public abstract class PlatformServices : INotifyPropertyChanged
{
	/// <inheritdoc/>
	public event PropertyChangedEventHandler? PropertyChanged;

	/// <summary>
	/// Gets or sets the view model services that may be necessary to perform certain operations.
	/// </summary>
	public IViewModelServices? ViewModelServices { protected get; set; }

	/// <summary>
	/// Gets a value indicating whether the platform provides a UI "back" button,
	/// such that the app's own UI need not display one.
	/// </summary>
	public virtual bool HasHardwareBackButton => false;

	/// <summary>
	/// Gets a value indicating whether an app-level menu exists where the About menu item can be placed.
	/// </summary>
	/// <remarks>
	/// This is expected to be <see langword="true"/> on MacOS.
	/// </remarks>
	public virtual bool HasAppLevelSystemMenu => false;

	/// <summary>
	/// Gets a value indicating whether the system is running on AC power.
	/// </summary>
	public abstract bool IsOnACPower { get; }

	/// <summary>
	/// Gets a value indicating whether the current network connection is metered.
	/// </summary>
	public abstract bool IsNetworkMetered { get; }

	/// <summary>
	/// Gets a value indicating whether the app is running under an automated test.
	/// </summary>
	public virtual bool IsRunningUnderTest => false;

	/// <summary>
	/// Gets a value indicating whether the app should offer to scan QR codes.
	/// </summary>
	public virtual bool CanSearchForQRCodes => this.ViewModelServices?.TopLevel?.StorageProvider.CanOpen is true;

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
	public abstract IDisposable? RequestSleepDeferral();

	/// <summary>
	/// Creates a view model capable of capturing and processing images from a camera or photo library
	/// and scanning them for QR codes.
	/// </summary>
	/// <returns>
	/// The view model, if one is supported.
	/// </returns>
	/// <remarks>
	/// The caller should first check <see cref="CanSearchForQRCodes"/> at least once for the lifetime of the app.
	/// </remarks>
	public virtual CameraViewModel? CreateCameraViewModel()
		=> this.ViewModelServices is not null ? new CameraViewModel(this.ViewModelServices) : null;

	/// <summary>
	/// Raises the <see cref="PropertyChanged"/> event.
	/// </summary>
	/// <param name="propertyName">The name of the property whose value has changed.</param>
	protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
		=> this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

	protected void RaiseAndSetIfChanged<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
	{
		if (!EqualityComparer<T>.Default.Equals(field, value))
		{
			field = value;
			this.OnPropertyChanged(propertyName);
		}
	}
}
