// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Microsoft.VisualStudio.Threading;
using Nerdbank.QRCodes;

namespace Nerdbank.Zcash.App.ViewModels;

public class CameraViewModel : ViewModelBase, IDisposable
{
	private readonly TaskCompletionSource<string?> scannedText = new();
	private readonly IViewModelServices viewModelServices;
	private Camera? selectedCamera;
	private IImage? capturedImage;

	[Obsolete("Design-time only", error: true)]
	public CameraViewModel()
		: this(new DesignTimeViewModelServices())
	{
		this.Cameras.Add(new Camera("Front"));
		this.Cameras.Add(new Camera("Back"));
	}

	public CameraViewModel(IViewModelServices viewModelServices)
	{
		this.viewModelServices = viewModelServices;

		ObservableBox<bool> canSelectPhoto = new(viewModelServices.TopLevel?.StorageProvider.CanOpen is true);
		this.SelectPhotoFromLibraryCommand = ReactiveCommand.CreateFromTask(this.SelectPhotoFromLibraryAsync, canSelectPhoto);
		this.Cameras.CollectionChanged += (sender, e) =>
		{
			if (this.SelectedCamera is not null && e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove && e.OldItems?.Contains(this.SelectedCamera) is true)
			{
				// Unselect a camera that has been removed.
				this.SelectedCamera = null;
			}

			if (this.Cameras.Count > 0 && this.SelectedCamera is null)
			{
				// Select the first available camera when no camera is available.
				this.SelectedCamera = this.Cameras[0];
			}
		};
	}

	~CameraViewModel()
	{
		this.Dispose(false);
	}

	public string SelectPhotoFromLibraryCaption => CameraStrings.SelectPhotoFromLibraryCaption;

	public ICommand SelectPhotoFromLibraryCommand { get; }

	public string UserPrompt => CameraStrings.UserPrompt;

	public ObservableCollection<Camera> Cameras { get; } = new();

	public Camera? SelectedCamera
	{
		get => this.selectedCamera;
		set
		{
			if (this.selectedCamera != value)
			{
				if (this.selectedCamera is not null)
				{
					this.selectedCamera.IsActivated = false;
					this.selectedCamera.PropertyChanged -= this.Camera_PropertyChanged;
				}

				this.selectedCamera = value;
				this.RaisePropertyChanged();
				this.CapturedImage = null;

				if (value is not null)
				{
					value.PropertyChanged += this.Camera_PropertyChanged;
					value.IsActivated = true;
				}
			}
		}
	}

	public IImage? CapturedImage
	{
		get => this.capturedImage;
		set => this.RaiseAndSetIfChanged(ref this.capturedImage, value);
	}

	protected virtual JoinableTask CamerasInitialized => this.viewModelServices.App.JoinableTaskContext.Factory.RunAsync(() => Task.CompletedTask);

	public async Task SelectPhotoFromLibraryAsync(CancellationToken cancellationToken)
	{
		Verify.Operation(this.viewModelServices.TopLevel is not null, "This command is not available.");
		IReadOnlyList<IStorageFile> selectedFiles = await this.viewModelServices.TopLevel.StorageProvider.OpenFilePickerAsync(new()
		{
			AllowMultiple = false,
			Title = "Select a photo to scan for a QR code",
			FileTypeFilter =
			[
				new("Photos")
				{
					Patterns = ["*.jpg", "*.jpeg", "*.png", "*.bmp"],
					MimeTypes = ["image/jpeg", "image/png", "image/bmp"],
				},
			],
		});

		try
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (selectedFiles.Count > 0)
			{
				using Stream photoStream = await selectedFiles[0].OpenReadAsync();
				string text = await this.ScanPhotoAsync(photoStream, cancellationToken);

				// We report our result whether a QR code was found or not
				// to differentiate from a canceled file picker dialog.
				this.SetResult(text);
			}
		}
		finally
		{
			foreach (IStorageFile file in selectedFiles)
			{
				file.Dispose();
			}
		}
	}

	/// <summary>
	/// Acquires text from a QR code by scanning it with the camera or from an existing photo.
	/// </summary>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <exception cref="OperationCanceledException">Thrown if <paramref name="cancellationToken"/> is canceled.</exception>
	/// <returns>
	/// A non-empty string if a QR code could be found decoded;
	/// an empty string if no QR code could be found;
	/// <see langword="null" /> if the operation was canceled.
	/// </returns>
	public async Task<string?> GetTextFromQRCodeAsync(CancellationToken cancellationToken)
	{
		Verify.Operation(this.viewModelServices.TopLevel is not null, "This command is not available.");

		// Only show the camera view if there is a camera available on the device
		// (and the platform-derivation of this class supports it).
		await this.CamerasInitialized;
		if (this.Cameras.Count > 0)
		{
			this.viewModelServices.NavigateTo(this);
		}
		else
		{
			// Otherwise, skip to photo selection from library.
			await this.SelectPhotoFromLibraryAsync(cancellationToken);

			// In case the operation above was canceled or failed to find a QR code,
			// Indicate we've aborted so we don't hang the command.
			this.scannedText.TrySetResult(null);
		}

#pragma warning disable VSTHRD003 // Avoid awaiting foreign Tasks
		return await this.scannedText.Task;
#pragma warning restore VSTHRD003 // Avoid awaiting foreign Tasks
	}

	public void Dispose()
	{
		this.Dispose(true);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (disposing)
		{
			this.scannedText.TrySetCanceled();

			if (this.SelectedCamera is not null)
			{
				this.SelectedCamera.IsActivated = false;
				this.SelectedCamera = null;
			}
		}
	}

	protected void SetResult(string? text)
	{
		this.scannedText.TrySetResult(text);
		this.viewModelServices.NavigateBack(this);
	}

	protected virtual async Task<string> ScanPhotoAsync(Stream photoStream, CancellationToken cancellationToken)
	{
		MemoryStream ms = new();
		await photoStream.CopyToAsync(ms, cancellationToken);
		return Helper(ms);

		static string Helper(MemoryStream s)
		{
			Span<char> decoded = stackalloc char[1024];
			Span<byte> image = s.GetBuffer().AsSpan(0, (int)s.Length);
			return QRDecoder.TryDecode(image, out string? text) ? text : string.Empty;
		}
	}

	private void Camera_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (sender != this.SelectedCamera || sender is null)
		{
			return;
		}

		switch (e.PropertyName)
		{
			case nameof(Camera.CurrentFrame):
				IImage? oldFrame = this.CapturedImage;
				if (((Camera)sender).CurrentFrame is Frame frame)
				{
					this.CapturedImage = new Bitmap(new MemoryStream(frame.EncodedImage));

					// Look for a QR code in this frame.
					if (QRDecoder.TryDecode(frame.EncodedImage, out string? data))
					{
						this.SetResult(data);
					}
				}
				else
				{
					this.CapturedImage = null;
				}

				(oldFrame as IDisposable)?.Dispose();
				break;
		}
	}

	public class Camera(string name) : ReactiveObject
	{
		private Frame? currentFrame;
		private bool isActivated;

		public string Name => name;

		public Frame? CurrentFrame
		{
			get => this.currentFrame;
			set => this.RaiseAndSetIfChanged(ref this.currentFrame, value);
		}

		public bool IsActivated
		{
			get => this.isActivated;
			set
			{
				if (this.isActivated != value)
				{
					this.isActivated = value;
					this.RaisePropertyChanged();
					this.OnActivationChange();
				}
			}
		}

		public override string ToString() => name;

		protected virtual void OnActivationChange()
		{
		}
	}

	public record Frame(byte[] EncodedImage);
}
