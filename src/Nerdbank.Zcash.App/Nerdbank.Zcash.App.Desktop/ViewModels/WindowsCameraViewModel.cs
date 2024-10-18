// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if WINDOWS

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Threading;
using Nerdbank.Zcash.App.ViewModels;
using Windows.Devices.Enumeration;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.MediaProperties;

namespace Nerdbank.Zcash.App.Desktop.ViewModels;

internal class WindowsCameraViewModel : CameraViewModel, IDisposable
{
	private readonly CancellationTokenSource disposalTokenSource = new();
	private readonly AsyncManualResetEvent disposalEvent = new();
	private readonly ILogger logger;
	private readonly JoinableTask camerasPopulation;
	private readonly ConcurrentDictionary<string, WindowsCamera> camerasByGroupId = new();
	private readonly ConcurrentDictionary<string, WindowsCamera> camerasBySourceId = new();
	private readonly IViewModelServices viewModelServices;

	[Obsolete("Design-time only", error: true)]
	public WindowsCameraViewModel()
			: this(new DesignTimeViewModelServices())
	{
	}

	public WindowsCameraViewModel(IViewModelServices viewModelServices)
		: base(viewModelServices)
	{
		this.viewModelServices = viewModelServices;
		this.logger = viewModelServices.App.PlatformServices.LoggerFactory.CreateLogger<WindowsCameraViewModel>();
		this.camerasPopulation = viewModelServices.App.JoinableTaskContext.Factory.RunAsync(
			() => this.PopulateCamerasCollectionAsync(this.disposalTokenSource.Token));
		this.camerasPopulation.Task.LogFaults(this.logger, "Initial camera population failed.");
		this.WatchCamerasCollectionAsync(this.disposalTokenSource.Token).LogFaults(this.logger, "Watcher of available cameras failed.");
	}

	protected override JoinableTask CamerasInitialized => this.camerasPopulation;

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			this.disposalTokenSource.Cancel();
			this.disposalEvent.Set();
		}

		base.Dispose(true);
	}

	private static bool IsAcceptableSource(MediaFrameSourceInfo sourceInfo)
		=> sourceInfo.SourceKind == MediaFrameSourceKind.Color && sourceInfo.MediaStreamType is MediaStreamType.Photo or MediaStreamType.VideoRecord or MediaStreamType.VideoPreview;

	private async Task PopulateCamerasCollectionAsync(CancellationToken cancellationToken)
	{
		IReadOnlyList<MediaFrameSourceGroup> allGroups = await MediaFrameSourceGroup.FindAllAsync();
		cancellationToken.ThrowIfCancellationRequested();
		foreach (MediaFrameSourceGroup group in allGroups)
		{
			this.AddCameraIfAcceptable(group);
		}
	}

	private void AddCameraIfAcceptable(MediaFrameSourceGroup group)
	{
		MediaFrameSourceInfo? acceptableSource = group.SourceInfos.FirstOrDefault(IsAcceptableSource);
		if (acceptableSource is null)
		{
			return;
		}

		// Some sources are available in multiple groups. Is the source we found in a group we already know about?
		if (this.camerasBySourceId.TryGetValue(acceptableSource.Id, out WindowsCamera? existingCamera))
		{
			// Keep/add the group that has the fewest sources because that tends to have the more human-recognizable name.
			if (existingCamera.SourceGroup.SourceInfos.Count < group.SourceInfos.Count)
			{
				// Keep the existing group.
				return;
			}
			else
			{
				// Remove the group we found before, because this one has fewer sources.
				this.camerasByGroupId.TryRemove(existingCamera.SourceGroup.Id, out _);
				this.camerasBySourceId.TryRemove(acceptableSource.Id, out _);
				this.Cameras.Remove(existingCamera);
			}
		}

		WindowsCamera camera = new(group, this);
		if (this.camerasByGroupId.TryAdd(group.Id, camera))
		{
			this.camerasBySourceId.TryAdd(acceptableSource.Id, camera);
			this.Cameras.Add(camera);
		}
	}

	private async Task WatchCamerasCollectionAsync(CancellationToken cancellationToken)
	{
		// Watch for cameras to be added or removed.
		string deviceSelector = MediaFrameSourceGroup.GetDeviceSelector();
		DeviceWatcher watcher = DeviceInformation.CreateWatcher(deviceSelector);
		watcher.Added += Watcher_Added;
		watcher.Removed += Watcher_Removed;
		watcher.Start();
		try
		{
			await this.disposalEvent;
		}
		finally
		{
			watcher.Stop();
		}

#pragma warning disable VSTHRD100 // Avoid async void methods
		async void Watcher_Added(DeviceWatcher sender, DeviceInformation args)
		{
			try
			{
				if (this.camerasByGroupId.ContainsKey(args.Id))
				{
					return;
				}

				MediaFrameSourceGroup added = await MediaFrameSourceGroup.FromIdAsync(args.Id);
				await this.viewModelServices.App.JoinableTaskContext.Factory.SwitchToMainThreadAsync(this.disposalTokenSource.Token);
				this.AddCameraIfAcceptable(added);
			}
			catch (Exception ex)
			{
				this.logger.LogError("Failure processing added camera: {ex}", ex.Message);
			}
		}

		async void Watcher_Removed(DeviceWatcher sender, DeviceInformationUpdate args)
		{
			WindowsCamera? camera = null;
			try
			{
				if (this.camerasByGroupId.TryRemove(args.Id, out camera))
				{
					foreach (MediaFrameSourceInfo source in camera.SourceGroup.SourceInfos)
					{
						this.camerasBySourceId.TryRemove(source.Id, out _);
					}

					camera.IsActivated = false;
					await this.viewModelServices.App.JoinableTaskContext.Factory.SwitchToMainThreadAsync(this.disposalTokenSource.Token);
					this.Cameras.Remove(camera);
				}
			}
			catch (Exception ex)
			{
				this.logger.LogError("Failure removing camera \"{camera}\": {ex}", camera?.Name, ex.Message);
			}
		}
#pragma warning restore VSTHRD100 // Avoid async void methods
	}

	private class WindowsCamera(MediaFrameSourceGroup sourceGroup, WindowsCameraViewModel owner) : Camera(sourceGroup.DisplayName)
	{
		private bool unexpectedFormatLogged;

		public MediaFrameSourceGroup SourceGroup => sourceGroup;

		protected override void OnActivationChange()
		{
			base.OnActivationChange();

			if (this.IsActivated)
			{
				this.CaptureAsync(owner.disposalTokenSource.Token).LogFaults(owner.logger, "Camera connection failed.");
			}
		}

		private async Task CaptureAsync(CancellationToken cancellationToken)
		{
			MediaCapture? mediaCapture = null;
			MediaFrameReader? frameReader = null;
			try
			{
				// Although we set the field, that's so this can be disposed of later.
				// To ensure we don't operate on a future selected camera, always use the local variable in this method.
				mediaCapture = new MediaCapture();

				MediaCaptureInitializationSettings settings = new()
				{
					SourceGroup = sourceGroup,
					SharingMode = MediaCaptureSharingMode.SharedReadOnly,
					StreamingCaptureMode = StreamingCaptureMode.Video,
					MemoryPreference = MediaCaptureMemoryPreference.Cpu,
				};

				await mediaCapture.InitializeAsync(settings);

				MediaFrameSource? source = mediaCapture.FrameSources.Values.FirstOrDefault(s => IsAcceptableSource(s.Info));
				if (source is null)
				{
					return;
				}

				frameReader = await mediaCapture.CreateFrameReaderAsync(source, MediaEncodingSubtypes.Bgra8);
				AsyncAutoResetEvent frameArrivedEvent = new();
				frameReader.FrameArrived += (s, e) => frameArrivedEvent.Set();

				MediaFrameReaderStartStatus status = await frameReader.StartAsync();
				if (status == MediaFrameReaderStartStatus.Success)
				{
					owner.logger.LogInformation("Started the \"{group}\" camera.", this.Name);
				}
				else
				{
					owner.logger.LogError("Unable to start the \"{group}\" camera. Error: {status}", this.Name, status);
				}

				while (!cancellationToken.IsCancellationRequested && this.IsActivated)
				{
					await frameArrivedEvent.WaitAsync(cancellationToken);

					// TryAcquireLatestFrame will return the latest frame that has not yet been acquired.
					// This can return null if there is no such frame, or if the reader is not in the
					// "Started" state. The latter can occur if a FrameArrived event was in flight
					// when the reader was stopped.
					using MediaFrameReference? frame = frameReader.TryAcquireLatestFrame();
					if (frame is not null)
					{
						await this.ApplyImageAsync(frame.VideoMediaFrame, cancellationToken);
					}
				}
			}
			finally
			{
				mediaCapture?.Dispose();
				frameReader?.Dispose();
			}
		}

		private async ValueTask ApplyImageAsync(VideoMediaFrame inputFrame, CancellationToken cancellationToken)
		{
			using SoftwareBitmap inputBitmap = inputFrame.SoftwareBitmap;
			if (inputBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8)
			{
				// Only log the error once so we don't spam this for every single frame.
				if (!this.unexpectedFormatLogged)
				{
					owner.logger.LogError("Camera {camera} produced unexpected format {format}.", this.Name, inputBitmap.BitmapPixelFormat);
					this.unexpectedFormatLogged = true;
				}

				return;
			}

			using SoftwareBitmap softwareBitmap = inputBitmap.BitmapAlphaMode switch
			{
				BitmapAlphaMode.Premultiplied => inputBitmap,
				_ => SoftwareBitmap.Convert(inputBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied),
			};

			cancellationToken.ThrowIfCancellationRequested();
			using Windows.Storage.Streams.InMemoryRandomAccessStream uwpStream = new();
			BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.BmpEncoderId, uwpStream);
			encoder.SetSoftwareBitmap(softwareBitmap);
			await encoder.FlushAsync();
			uwpStream.Seek(0);

			byte[] backBuffer = new byte[uwpStream.Size];
			using Stream stream = uwpStream.AsStream();
			await stream.ReadExactlyAsync(backBuffer, cancellationToken);

			this.CurrentFrame = new Frame(backBuffer);
		}
	}
}

#endif
