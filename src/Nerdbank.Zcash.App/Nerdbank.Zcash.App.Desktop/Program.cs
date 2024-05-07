﻿// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.ReactiveUI;
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

		AppBuilder appBuilder = BuildAvaloniaApp();

		OneProcessManager processManager = new();
		processManager.SecondaryProcessStarted += (sender, e) =>
		{
			if (e.CommandLineArgs is not null)
			{
				if (ZcashScheme.TryParseUriLaunch(e.CommandLineArgs, out Uri? zcashPaymentRequest))
				{
					// Navigate the app to the payment screen and initialize with content from the request URI.
					// If the payment screen is already displayed, we should prompt the user
					// to confirm the new request to avoid another app writing over a payment
					// just before it is made by the user.
				}
			}
		};

		if (processManager.TryClaimPrimaryProcess())
		{
			if (ZcashScheme.TryParseUriLaunch(args, out Uri? zcashPaymentRequest))
			{
				// Prepare the new application to respond to the payment request.
				// We must consider that the app may not have created a wallet yet,
				// in which case we need to instruct the user to create one.
			}

			return appBuilder.StartWithClassicDesktopLifetime(args);
		}
		else
		{
			return 0;
		}
	}

	// Avalonia configuration, don't remove; also used by visual designer.
	public static AppBuilder BuildAvaloniaApp()
	{
		IPlatformServices platformServices =
#if WINDOWS
			new WindowsPlatformServices();
#else
			new FallbackPlatformServices();
#endif
		AppBuilder builder = AppBuilder.Configure(() => new App(PrepareAppPlatformSettings(), platformServices, ThisAssembly.VelopackUpdateUrl))
			.UsePlatformDetect()
			.WithInterFont()
			.LogToTrace()
			.UseReactiveUI();

		// Workaround for transparent Window on win-arm64 (https://github.com/AvaloniaUI/Avalonia/issues/10405)
		if (OperatingSystem.IsWindows() && RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
		{
			builder = builder.UseWin32()
				.With(new Win32PlatformOptions
				{
					RenderingMode = new[] { Win32RenderingMode.Software },
				});
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
		string confidentialDataPath = Path.Combine(appDataBaseDir, "wallets");
		string nonConfidentialDataPath = Path.Combine(appDataBaseDir, "settings");

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

		// Create the directory for settings.
		Directory.CreateDirectory(nonConfidentialDataPath);

		return new AppPlatformSettings
		{
			ConfidentialDataPathIsEncrypted = encryptionSuccessful,
			ConfidentialDataPath = confidentialDataPath,
			NonConfidentialDataPath = nonConfidentialDataPath,
		};
	}
}
