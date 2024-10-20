// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if MACOS

using AVFoundation;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Threading;
using Nerdbank.Zcash.App.ViewModels;

namespace Nerdbank.Zcash.App.Desktop.ViewModels;

public class MacOSCameraViewModel : CameraViewModel
{
	[Obsolete("Design-time only", error: true)]
	public MacOSCameraViewModel()
	{
	}

	public MacOSCameraViewModel(IViewModelServices viewModelServices)
		: base(viewModelServices)
	{
		this.PopulateCamerasCollection();
		this.WatchCamerasCollectionAsync(this.DisposalToken).LogFaults(this.Logger, "Watcher of available cameras failed.");
	}

	private void AddCameraIfAcceptable(AVCaptureDevice device)
	{
		if (!device.Formats.Any(f => f.MediaType == "vide"))
		{
			return;
		}

		if (!this.Cameras.Any(c => ((MacOSCamera)c).Device.UniqueID == device.UniqueID))
		{
			this.Cameras.Add(new MacOSCamera(device, this));
		}
	}

	private void PopulateCamerasCollection()
	{
		AVCaptureDevice? defaultDevice = AVCaptureDevice.GetDefaultDevice(AVMediaTypes.Video);
		if (defaultDevice is not null)
		{
			this.AddCameraIfAcceptable(defaultDevice);
		}

		AVCaptureDeviceDiscoverySession session = AVCaptureDeviceDiscoverySession.Create(
			[],
			AVMediaTypes.Video,
			AVCaptureDevicePosition.Unspecified);
		foreach (AVCaptureDevice device in session.Devices)
		{
			this.AddCameraIfAcceptable(device);
		}
	}

	private async Task WatchCamerasCollectionAsync(CancellationToken cancellationToken)
	{
		NSObject connected = NSNotificationCenter.DefaultCenter.AddObserver(AVCaptureDevice.WasConnectedNotification, DeviceConnected);
		NSObject disconnected =
			NSNotificationCenter.DefaultCenter.AddObserver(AVCaptureDevice.WasDisconnectedNotification, DeviceDisconnected);

		try
		{
			await this.WaitForDisposalAsync(cancellationToken);
		}
		finally
		{
			NSNotificationCenter.DefaultCenter.RemoveObserver(connected);
			NSNotificationCenter.DefaultCenter.RemoveObserver(disconnected);
		}

		void DeviceConnected(NSNotification notification)
		{
			// A new camera comes in as an AVCaptureDALDevice, which is an internal type
			// that we cannot use or test for.
			// So instead, just search all the cameras for the new one.
			this.PopulateCamerasCollection();
		}

		void DeviceDisconnected(NSNotification notification)
		{
			if (notification.Object is AVCaptureDevice device &&
				this.Cameras.FirstOrDefault(c => ((MacOSCamera)c).Device.UniqueID == device.UniqueID) is MacOSCamera camera)
			{
				this.Logger.LogInformation($"Device disconnected: {device.LocalizedName}");
				this.Cameras.Remove(camera);
			}
		}
	}

	public class MacOSCamera(AVCaptureDevice device, MacOSCameraViewModel owner) : Camera(device.LocalizedName)
	{
		internal AVCaptureDevice Device => device;

		protected override void OnActivationChange()
		{
			base.OnActivationChange();

			if (this.IsActivated)
			{
				this.CaptureAsync(owner.DisposalToken).LogFaults(owner.Logger, "Camera connection failed.");
			}
		}

		private async Task CaptureAsync(CancellationToken cancellationToken)
		{
			AVCaptureDeviceInput? input = AVCaptureDeviceInput.FromDevice(device, out NSError error);
			if (error is not null)
			{
				owner.Logger.LogError(error.Description);
			}

			if (input is null)
			{
				return;
			}

			AVCaptureSession? session = null;
			AVCapturePhotoOutput? output = null;
			try
			{
				session = new();
				session.AddInput(input);
				session.SessionPreset = AVCaptureSession.PresetPhoto;
				output = new();
				session.AddOutput(output);
				session.StartRunning();

				while (!cancellationToken.IsCancellationRequested && this.IsActivated)
				{
					AVCapturePhotoSettings settings = AVCapturePhotoSettings.Create();
					CaptureCallback callback = new(this);
					output.CapturePhoto(settings, callback);
					await callback.Finished.WithCancellation(cancellationToken);
				}
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				owner.Logger.LogError(ex, "Failed to capture camera");
			}
			finally
			{
				session?.Dispose();
				output?.Dispose();
			}
		}

		private class CaptureCallback(MacOSCamera camera) : AVCapturePhotoCaptureDelegate
		{
			private readonly TaskCompletionSource<bool> finishedSource = new();

			internal Task Finished => this.finishedSource.Task;

			public override void DidFinishProcessingPhoto(AVCapturePhotoOutput output, AVCapturePhoto photo, NSError? error)
			{
				try
				{
					if (photo.FileDataRepresentation is null)
					{
						// This can happen when the computer goes to sleep.
						// Arrange to try again after a second.
						Task.Delay(1000).ContinueWith(
							_ => this.finishedSource.SetResult(false),
							CancellationToken.None,
							TaskContinuationOptions.None,
							TaskScheduler.Default).Forget();
						return;
					}

					using Stream? s = photo.FileDataRepresentation.AsStream();
					int l = checked((int)s.Length);
					byte[] buffer = new byte[l];
					s.ReadExactly(buffer, 0, buffer.Length);
					camera.CurrentFrame = new Frame(buffer);
					this.finishedSource.SetResult(true);
				}
				catch (Exception ex)
				{
					camera.CurrentFrame = null;
					this.finishedSource.SetException(ex);
				}
			}
		}
	}
}

#endif
