// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.ReactiveUI;
using Avalonia.Svg.Skia;
using Nerdbank.Zcash.App.ViewModels;
using Velopack;

namespace Nerdbank.Zcash.App.Desktop;

internal class Program
{
	private static readonly UriSchemeRegistration ZcashScheme = new("zcash");

	// Initialization code. Don't use any Avalonia, third-party APIs or any
	// SynchronizationContext-reliant code before AppMain is called: things aren't initialized
	// yet and stuff might break.
	[STAThread]
	public static int Main(string[] args)
	{
		VelopackApp velopackBuilder = VelopackApp.Build();

		if (OperatingSystem.IsWindows())
		{
			velopackBuilder.WithAfterInstallFastCallback(v =>
			{
				UriSchemeRegistration.Register(ZcashScheme);
			});
			velopackBuilder.WithBeforeUninstallFastCallback(v =>
			{
				UriSchemeRegistration.Unregister(ZcashScheme);
			});
		}

		velopackBuilder.Run();

		AppBuilder appBuilder = BuildAvaloniaApp(args);

		using OneProcessManager processManager = new();
		processManager.SecondaryProcessStarted += async (sender, e) =>
		{
			try
			{
				if (e.CommandLineArgs is not null && App.Current?.ViewModel is not null)
				{
					if (ZcashScheme.TryParseUriLaunch(e.CommandLineArgs, out Uri? zcashPaymentRequest))
					{
						await App.Current.JoinableTaskContext.Factory.SwitchToMainThreadAsync();
						SendingViewModel viewModel = new(App.Current.ViewModel);
						if (viewModel.TryApplyPaymentRequest(zcashPaymentRequest))
						{
							App.Current.ViewModel.NavigateTo(viewModel);
						}
					}
				}
			}
			catch
			{
				// Don't crash the app when failing to process such messages.
			}
		};

		if (processManager.TryClaimPrimaryProcess())
		{
			UriSchemeRegistration.Register(ZcashScheme);
			return appBuilder.StartWithClassicDesktopLifetime(args);
		}
		else
		{
			return 0;
		}
	}

	// Avalonia configuration, don't remove; also used by visual designer.
	public static AppBuilder BuildAvaloniaApp() => BuildAvaloniaApp([]);

	public static AppBuilder BuildAvaloniaApp(string[] args)
	{
		// Make SVG rendering work in the Avalonia previewer.
		GC.KeepAlive(typeof(SvgImageExtension).Assembly);
		GC.KeepAlive(typeof(Avalonia.Svg.Skia.Svg).Assembly);

		ZcashScheme.TryParseUriLaunch(args, out Uri? zcashPaymentRequest);
		StartupInstructions startup = new()
		{
			PaymentRequestUri = zcashPaymentRequest,
		};

		PlatformServices platformServices =
#if WINDOWS
			new WindowsPlatformServices();
#else
			OperatingSystem.IsMacOS() ? new MacOSPlatformServices() : new FallbackPlatformServices();
#endif

		string? updateSource = Environment.GetEnvironmentVariable("EZCASH_UPDATE_SOURCE");
		if (string.IsNullOrWhiteSpace(updateSource))
		{
			updateSource = ThisAssembly.VelopackUpdateUrl;
		}

		AppBuilder builder = AppBuilder.Configure(() => new App(PrepareAppPlatformSettings(), platformServices, startup, updateSource))
			.UsePlatformDetect()
			.WithInterFont()
			.LogToTrace()
			.UseReactiveUI();

		if (OperatingSystem.IsWindows())
		{
			Win32PlatformOptions options = new();

			if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
			{
				// Workaround for transparent Window on win-arm64 (https://github.com/AvaloniaUI/Avalonia/issues/10405)
				options.RenderingMode = [Win32RenderingMode.Software];
			}

			builder.UseWin32().With(options);
		}
		else if (OperatingSystem.IsMacOS())
		{
			builder.With(new MacOSPlatformOptions { });
		}

		return builder;
	}

	private static AppPlatformSettings PrepareAppPlatformSettings()
	{
		if (Design.IsDesignMode)
		{
			// When running in the designer, we shouldn't try to access the files on the user's installation.
			return new()
			{
				ConfidentialDataPath = null,
				NonConfidentialDataPath = null,
			};
		}

		string appDataBaseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nerdbank.Zcash.App");
		string nonConfidentialDataPath = Path.Combine(appDataBaseDir, "settings");

		// Velopack installs the app under appDataBaseDir.
		// We MUST NOT store wallets under that same directory or velopack's repair and uninstall steps
		// will quietly and suddenly delete the wallets.
		string confidentialDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nerdbank.Zcash.App.Wallets");

		// Find the appropriate path for storing wallets.
		// Create the directory and try setting it to encrypt its contents via NTFS attributes if available.
		bool encryptionSuccessful = false;
		DirectoryInfo dirInfo = Directory.CreateDirectory(confidentialDataPath);
		if (Directory.Exists(confidentialDataPath))
		{
			encryptionSuccessful = (dirInfo.Attributes & FileAttributes.Encrypted) == FileAttributes.Encrypted;
		}

		if (!encryptionSuccessful && OperatingSystem.IsWindows())
		{
			try
			{
				File.Encrypt(confidentialDataPath);
				encryptionSuccessful = true;
			}
			catch (PlatformNotSupportedException)
			{
				// NTFS encryption not supported on this platform.
			}
			catch (IOException)
			{
				// NTFS encryption not supported on this platform.
			}
		}

		// Migrate from the old wallets location if it exists.
		MigrateWalletsLocation(appDataBaseDir, confidentialDataPath);

		// Create the directory for settings.
		Directory.CreateDirectory(nonConfidentialDataPath);

		return new AppPlatformSettings
		{
			ConfidentialDataPathIsEncrypted = encryptionSuccessful,
			ConfidentialDataPath = confidentialDataPath,
			NonConfidentialDataPath = nonConfidentialDataPath,
		};
	}

	private static void MigrateWalletsLocation(string appDataBaseDir, string confidentialDataPath)
	{
		string oldConfidentialDataPath = Path.Combine(appDataBaseDir, "wallets");
		if (Directory.Exists(oldConfidentialDataPath))
		{
			try
			{
				foreach (string oldFile in Directory.EnumerateFiles(oldConfidentialDataPath))
				{
					string newFile = Path.Combine(confidentialDataPath, Path.GetFileName(oldFile));
					if (!File.Exists(newFile))
					{
						File.Move(oldFile, newFile);
					}
				}

				Directory.Delete(oldConfidentialDataPath);
			}
			catch
			{
				// It was best effort anyway.
			}
		}
	}
}
