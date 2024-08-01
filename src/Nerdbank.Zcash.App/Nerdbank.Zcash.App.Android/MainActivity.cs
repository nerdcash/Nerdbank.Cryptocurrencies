// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using Android.Content.PM;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using Avalonia.Controls;
using Avalonia.ReactiveUI;

namespace Nerdbank.Zcash.App.Android;

[Activity(
	Label = "eZcash",
	Theme = "@style/MyTheme.NoActionBar",
	Icon = "@drawable/icon",
	MainLauncher = true,
	ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<App>
{
	protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
	{
		AndroidEnvironment.UnhandledExceptionRaiser += (s, e) =>
		{
			Debug.WriteLine(e.Exception);
		};

		// This is new in Avalonia 11.1.0-beta1
		////Dispatcher.UIThread.UnhandledException += (s, e) =>
		////{
		////	Debug.WriteLine(e.Exception);
		////};

		// Discard the builder we're given because we want to call the non-default App constructor.
		return base.CustomizeAppBuilder(AppBuilder.Configure(
			() => new App(PrepareAppPlatformSettings(), new AndroidPlatformServices(this), null, null)))
			.WithInterFont()
			.UseAndroid()
			.UseReactiveUI();
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
