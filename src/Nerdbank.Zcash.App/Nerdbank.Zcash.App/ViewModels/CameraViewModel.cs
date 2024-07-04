// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Platform.Storage;

namespace Nerdbank.Zcash.App.ViewModels;

public class CameraViewModel : ViewModelBase, IDisposable
{
	private readonly TaskCompletionSource<string?> scannedText = new();
	private readonly IViewModelServices viewModelServices;
	private Camera? selectedCamera;

	[Obsolete("Design-time only", error: true)]
	public CameraViewModel()
		: this(new DesignTimeViewModelServices())
	{
		this.Cameras = new([new("Front"), new("Back")]);
	}

	public CameraViewModel(IViewModelServices viewModelServices)
	{
		this.viewModelServices = viewModelServices;

		ObservableBox<bool> canSelectPhoto = new(viewModelServices.TopLevel?.StorageProvider.CanOpen is true);
		this.SelectPhotoFromLibraryCommand = ReactiveCommand.CreateFromTask(this.SelectPhotoFromLibraryAsync, canSelectPhoto);

		this.Cameras = new([]);
		this.selectedCamera = this.Cameras.FirstOrDefault();
	}

	public string SelectPhotoFromLibraryCaption => CameraStrings.SelectPhotoFromLibraryCaption;

	public ICommand SelectPhotoFromLibraryCommand { get; }

	public string UserPrompt => CameraStrings.UserPrompt;

	public ReadOnlyCollection<Camera> Cameras { get; protected set; }

	public Camera? SelectedCamera
	{
		get => this.selectedCamera;
		set => this.RaiseAndSetIfChanged(ref this.selectedCamera, value);
	}

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
		this.scannedText.TrySetCanceled();
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
			uint decodedLength = NativeMethods.DecodeQrCodeFromImage(image, decoded);
			if (decodedLength > decoded.Length)
			{
				decoded = new char[decodedLength];
				decodedLength = NativeMethods.DecodeQrCodeFromImage(image, decoded);
			}

			return decodedLength > 0 ? new string(decoded[..checked((int)decodedLength)]) : string.Empty;
		}
	}

	public class Camera(string name)
	{
		public string Name => name;

		public override string ToString() => name;
	}
}
