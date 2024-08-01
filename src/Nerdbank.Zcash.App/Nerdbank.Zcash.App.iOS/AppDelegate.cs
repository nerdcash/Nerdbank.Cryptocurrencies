// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.iOS;
using Avalonia.ReactiveUI;

namespace Nerdbank.Zcash.App.iOS;

// The UIApplicationDelegate for the application. This class is responsible for launching the
// User Interface of the application, as well as listening (and optionally responding) to
// application events from iOS.
[Register("AppDelegate")]
public partial class AppDelegate : AvaloniaAppDelegate<App>
{
	protected override AppBuilder CreateAppBuilder()
	{
		return AppBuilder.Configure(
			() => new App(PrepareAppPlatformSettings(), new IosPlatformServices(this), null, null))
			.WithInterFont()
			.UseReactiveUI()
			.With(new iOSPlatformOptions { RenderingMode = [iOSRenderingMode.Metal] })
			.UseiOS();
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

		return new AppPlatformSettings
		{
			ConfidentialDataPathIsEncrypted = true, // iOS always encrypts user data.
			ConfidentialDataPath = confidentialDataPath,
			NonConfidentialDataPath = nonConfidentialDataPath,
		};
	}
}
