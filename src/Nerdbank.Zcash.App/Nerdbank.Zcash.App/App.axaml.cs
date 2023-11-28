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

	private AutoSaveManager<AppSettings>? appSettingsManager;
	private AppSettings? settings;
	private DataRoot? data;
	private AutoSaveManager<DataRoot>? dataRootManager;
	private WalletSyncManager? walletSyncManager;

	[Obsolete("Design-time only", error: true)]
	public App()
		: this(CreateDesignTimeAppPlatformSettings())
	{
		Assumes.NotNull(SynchronizationContext.Current);
		this.InitializeFields();
	}

	public App(AppPlatformSettings platformSettings)
	{
		this.AppPlatformSettings = platformSettings;
	}

	public AppSettings Settings => this.settings ?? throw new InvalidOperationException();

	public DataRoot Data => this.data ?? throw new InvalidOperationException();

	public AppPlatformSettings AppPlatformSettings { get; }

	/// <inheritdoc/>
	public override void Initialize()
	{
		AvaloniaXamlLoader.Load(this);
	}

	/// <inheritdoc/>
	public override void OnFrameworkInitializationCompleted()
	{
		this.InitializeFields();

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

	/// <summary>
	/// Initializes an application. Useful for unit tests to directly call.
	/// </summary>
	public void InitializeFields()
	{
		if (this.appSettingsManager is not null)
		{
			return;
		}

		this.appSettingsManager = this.AppPlatformSettings.NonConfidentialDataPath is not null ? AutoSaveManager<AppSettings>.LoadOrCreate(Path.Combine(this.AppPlatformSettings.NonConfidentialDataPath, SettingsJsonFileName), enableAutoSave: true) : null;
		this.dataRootManager = this.AppPlatformSettings.ConfidentialDataPath is not null ? AutoSaveManager<DataRoot>.LoadOrCreate(Path.Combine(this.AppPlatformSettings.ConfidentialDataPath, DataFileName), enableAutoSave: true) : null;
		this.settings = this.appSettingsManager?.Data ?? new AppSettings();
		this.data = this.dataRootManager?.Data ?? new DataRoot();

		if (this.AppPlatformSettings.ConfidentialDataPath is not null)
		{
			this.walletSyncManager = new WalletSyncManager(this.AppPlatformSettings.ConfidentialDataPath, this.Data.Wallet, this.Settings, this.Data.ContactManager);
		}
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
