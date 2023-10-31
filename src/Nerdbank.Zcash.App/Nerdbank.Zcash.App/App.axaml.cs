// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Nerdbank.Zcash.App.Views;

namespace Nerdbank.Zcash.App
{
	public partial class App : Application
	{
		[Obsolete("Design-time only", error: true)]
		public App()
		{
			this.Settings = new();
			this.AppPlatformSettings = new()
			{
				ConfidentialDataPath = null,
				NonConfidentialDataPath = null,
			};
		}

		public App(AppPlatformSettings platformSettings)
		{
			this.AppPlatformSettings = platformSettings;
			this.Settings = platformSettings.NonConfidentialDataPath is null ? new() : AppSettings.LoadOrCreate(Path.Combine(platformSettings.NonConfidentialDataPath, "settings.json"), enableAutoSave: true);
		}

		public static App Instance => (App?)Current ?? throw new InvalidOperationException("No app!");

		public AppSettings Settings { get; }

		public AppPlatformSettings AppPlatformSettings { get; }

		/// <inheritdoc/>
		public override void Initialize()
		{
			AvaloniaXamlLoader.Load(this);
		}

		/// <inheritdoc/>
		public override void OnFrameworkInitializationCompleted()
		{
			MainViewModel mainViewModel;
			if (this.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
			{
				desktop.MainWindow = new MainWindow
				{
					DataContext = mainViewModel = new MainWindowViewModel(),
				};
				mainViewModel.TopLevel = TopLevel.GetTopLevel(desktop.MainWindow);
			}
			else if (this.ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
			{
				singleViewPlatform.MainView = new MainView
				{
					DataContext = mainViewModel = new MainViewModel(),
				};
				mainViewModel.TopLevel = TopLevel.GetTopLevel(singleViewPlatform.MainView);
			}

			base.OnFrameworkInitializationCompleted();
		}
	}
}
