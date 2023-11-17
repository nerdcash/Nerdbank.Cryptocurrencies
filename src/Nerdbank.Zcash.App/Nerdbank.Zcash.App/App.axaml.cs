// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Nerdbank.Zcash.App.Views;

namespace Nerdbank.Zcash.App;

public partial class App : Application, IAsyncDisposable
{
	private const string DataFileName = "wallet.dat";
	private const string SettingsJsonFileName = "settings.json";

	private readonly AutoSaveManager<AppSettings>? appSettingsManager;
	private readonly AutoSaveManager<DataRoot>? dataRootManager;
	private readonly WalletSyncManager? walletSyncManager;

	[Obsolete("Design-time only", error: true)]
	public App()
		: this(CreateDesignTimeAppPlatformSettings())
	{
	}

	public App(AppPlatformSettings platformSettings)
	{
		this.AppPlatformSettings = platformSettings;

		this.appSettingsManager = platformSettings.NonConfidentialDataPath is not null ? AutoSaveManager<AppSettings>.LoadOrCreate(Path.Combine(platformSettings.NonConfidentialDataPath, SettingsJsonFileName), enableAutoSave: true) : null;
		this.dataRootManager = platformSettings.ConfidentialDataPath is not null ? AutoSaveManager<DataRoot>.LoadOrCreate(Path.Combine(platformSettings.ConfidentialDataPath, DataFileName), enableAutoSave: true) : null;

		this.Settings = this.appSettingsManager?.Data ?? new AppSettings();
		this.Data = this.dataRootManager?.Data ?? new DataRoot();

		if (platformSettings.ConfidentialDataPath is not null)
		{
			this.walletSyncManager = new WalletSyncManager(platformSettings.ConfidentialDataPath, this.Data.Wallet, this.Settings, this.Data.ContactManager);
		}
	}

	public AppSettings Settings { get; }

	public DataRoot Data { get; }

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
				DataContext = mainViewModel = new MainWindowViewModel(this),
			};

			// Give ourselves a chance to clean up nicely.
			desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
			desktop.MainWindow.Closed += async (_, _) =>
			{
				await this.DisposeAsync();
				desktop.Shutdown();
			};

			mainViewModel.TopLevel = TopLevel.GetTopLevel(desktop.MainWindow);
		}
		else if (this.ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
		{
			singleViewPlatform.MainView = new MainView
			{
				DataContext = mainViewModel = new MainViewModel(this),
			};
			mainViewModel.TopLevel = TopLevel.GetTopLevel(singleViewPlatform.MainView);
		}

		base.OnFrameworkInitializationCompleted();
	}

	public async ValueTask DisposeAsync()
	{
		if (this.walletSyncManager is not null)
		{
			await this.walletSyncManager.DisposeAsync();
		}

		if (this.appSettingsManager is not null)
		{
			await this.appSettingsManager.DisposeAsync();
		}

		if (this.dataRootManager is not null)
		{
			await this.dataRootManager.DisposeAsync();
		}
	}

	public void StartSync()
	{
		this.walletSyncManager?.StartSyncing(this.Data.Wallet);
	}

	internal static AppPlatformSettings CreateDesignTimeAppPlatformSettings()
	{
		return new()
		{
			ConfidentialDataPath = null,
			NonConfidentialDataPath = null,
		};
	}
}
