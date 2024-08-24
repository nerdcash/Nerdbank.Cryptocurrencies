// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Microsoft.VisualStudio.Threading;
using Nerdbank.Zcash.App.Views;
using IAsyncDisposable = System.IAsyncDisposable;

namespace Nerdbank.Zcash.App;

public partial class App : Application, IAsyncDisposable
{
	private const string SentryDsn = "https://b7355ed0f54921502d35449d93e1dfe3@sentry.ironpigeon.net/2";
	private const string DataFileName = "wallet.dat";
	private const string SettingsJsonFileName = "settings.json";

	private readonly List<Task> sendTransactionTasks = new();
	private readonly CancellationTokenSource shutdownTokenSource = new();

	private JoinableTaskContext? joinableTaskContext;
	private JoinableTaskFactory? shutdownBlockingTaskFactory;
	private JoinableTaskCollection? shutdownBlockingTasks;

	private StartupInstructions? startupInstructions;
	private AutoSaveManager<AppSettings>? appSettingsManager;
	private AppSettings? settings;
	private DataRoot? data;
	private AutoSaveManager<DataRoot>? dataRootManager;

	[Obsolete("Design-time only", error: true)]
	public App()
		: this(CreateDesignTimeAppPlatformSettings(), new DesignTimePlatformServices(), null, null)
	{
		Assumes.NotNull(SynchronizationContext.Current);
		this.InitializeFields();
	}

	public App(AppPlatformSettings platformSettings, PlatformServices platformServices, StartupInstructions? startupInstructions, string? velopackUpdateUrl)
	{
		if (!platformServices.IsRunningUnderTest)
		{
			SentrySdk.Init(options =>
			{
				// Tells which project in Sentry to send events to:
				// A Sentry Data Source Name (DSN) is required.
				// See https://docs.sentry.io/concepts/key-terms/dsn-explainer/
				// You can set it in the SENTRY_DSN environment variable, or you can set it in code here.
				options.Dsn = SentryDsn;

				// When configuring for the first time, to see what the SDK is doing:
				options.Debug = false;

				// This option is recommended. It enables Sentry's "Release Health" feature.
				options.AutoSessionTracking = true;

				// Enabling this option is recommended for client applications only. It ensures all threads use the same global scope.
				options.IsGlobalModeEnabled = false;
			});
		}

		this.AppPlatformSettings = platformSettings;
		this.PlatformServices = platformServices;
		this.startupInstructions = startupInstructions;
		this.SelfUpdating = new(this, velopackUpdateUrl);
	}

	public static new App? Current => (App?)Application.Current;

	public JoinableTaskContext JoinableTaskContext => this.joinableTaskContext ?? throw new InvalidOperationException();

	public WalletSyncManager? WalletSyncManager { get; private set; }

	public bool IsDesignTime => this.PlatformServices is DesignTimePlatformServices;

	public AppSettings Settings => this.settings ?? throw new InvalidOperationException();

	public AppUpdateManager SelfUpdating { get; }

	public DataRoot Data => this.data ?? throw new InvalidOperationException();

	public AppPlatformSettings AppPlatformSettings { get; }

	public PlatformServices PlatformServices { get; }

	public MainViewModel? ViewModel { get; private set; }

	/// <inheritdoc/>
	public override void Initialize()
	{
		AvaloniaXamlLoader.Load(this);
	}

	/// <inheritdoc/>
	public override void OnFrameworkInitializationCompleted()
	{
		this.InitializeFields();

		if (this.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			desktop.MainWindow = new MainWindow
			{
				DataContext = this.ViewModel = new MainWindowViewModel(this),
			};

			// Give ourselves a chance to clean up nicely.
			desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
			desktop.MainWindow.Closed += (_, _) => this.DisposeAsync().Forget();

			if (this.SelfUpdating is not null)
			{
				_ = this.shutdownBlockingTaskFactory!.RunAsync(() => this.SelfUpdating.PeriodicallyCheckForUpdatesAsync(this.shutdownTokenSource.Token));
			}

			this.ViewModel.TopLevel = TopLevel.GetTopLevel(desktop.MainWindow);
		}
		else if (this.ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
		{
			DeferredTopLevel viewModel = new(this);
			this.ViewModel = viewModel;
			singleViewPlatform.MainView = new MainView
			{
				DataContext = viewModel,
			};
			viewModel.TopVisual = singleViewPlatform.MainView;
		}

		base.OnFrameworkInitializationCompleted();

		this.HandleStartupInstructions();
	}

	/// <summary>
	/// Initializes an application. Useful for unit tests to directly call.
	/// </summary>
	public void InitializeFields()
	{
		if (this.joinableTaskContext is not null)
		{
			return;
		}

		// Tests don't set this.
		////Assumes.NotNull(SynchronizationContext.Current);

		this.joinableTaskContext = new JoinableTaskContext();
		this.shutdownBlockingTasks = this.joinableTaskContext.CreateCollection();
		this.shutdownBlockingTaskFactory = this.joinableTaskContext.CreateFactory(this.shutdownBlockingTasks);

		this.appSettingsManager = this.AppPlatformSettings.NonConfidentialDataPath is not null ? AutoSaveManager<AppSettings>.LoadOrCreate(Path.Combine(this.AppPlatformSettings.NonConfidentialDataPath, SettingsJsonFileName), enableAutoSave: true) : null;
		this.dataRootManager = this.AppPlatformSettings.ConfidentialDataPath is not null ? AutoSaveManager<DataRoot>.LoadOrCreate(Path.Combine(this.AppPlatformSettings.ConfidentialDataPath, DataFileName), enableAutoSave: true) : null;
		this.settings = this.appSettingsManager?.Data ?? new AppSettings();

		this.settings.PropertyChanged += this.Settings_PropertyChanged;
		this.SetThemeBasedOnSetting();

		this.data = this.dataRootManager?.Data ?? new DataRoot();

		if (this.AppPlatformSettings.ConfidentialDataPath is not null)
		{
			this.WalletSyncManager = new WalletSyncManager(this.joinableTaskContext, this.AppPlatformSettings.ConfidentialDataPath, this.Data.Wallet, this.Settings, this.Data.ContactManager, this.Data.ExchangeRates, this.PlatformServices);
		}
	}

	public async ValueTask DisposeAsync()
	{
		await this.shutdownTokenSource.CancelAsync();

		await this.WaitForSendsAsync();

		if (this.WalletSyncManager is not null)
		{
			await this.WalletSyncManager.DisposeAsync();
		}

		if (this.appSettingsManager is not null)
		{
			await this.appSettingsManager.DisposeAsync();
		}

		if (this.dataRootManager is not null)
		{
			await this.dataRootManager.DisposeAsync();
		}

		if (this.shutdownBlockingTasks is not null)
		{
			await this.shutdownBlockingTasks.JoinTillEmptyAsync();
		}

		if (this.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			desktop.Shutdown();
		}
	}

	public void RegisterSendTransactionTask(Task sendTask)
	{
		this.sendTransactionTasks.Add(sendTask);
		_ = sendTask.ContinueWith(this.sendTransactionTasks.Remove, TaskScheduler.FromCurrentSynchronizationContext());
	}

	internal static AppPlatformSettings CreateDesignTimeAppPlatformSettings()
	{
		return new()
		{
			ConfidentialDataPath = @"C:\some\path\to\wallet.dat",
			NonConfidentialDataPath = @"C:\some\path\to\settings",
		};
	}

	private void Settings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		switch (e.PropertyName)
		{
			case nameof(AppSettings.ThemeName):
				this.SetThemeBasedOnSetting();

				break;
		}
	}

	private void SetThemeBasedOnSetting()
	{
		ThemeVariant? theme = this.Settings.ThemeName switch
		{
			"Light" => ThemeVariant.Light,
			"Dark" => ThemeVariant.Dark,
			"Default" => ThemeVariant.Default,
			_ => null,
		};

		if (theme is not null)
		{
			this.RequestedThemeVariant = theme;
		}
	}

	private async Task WaitForSendsAsync()
	{
		while (this.sendTransactionTasks.Count > 0)
		{
			await Task.WhenAll(this.sendTransactionTasks).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
		}
	}

	private void HandleStartupInstructions()
	{
		if (this.ViewModel is not null && this.startupInstructions?.PaymentRequestUri is Uri paymentRequest)
		{
			SendingViewModel viewModel = new(this.ViewModel);
			if (viewModel.TryApplyPaymentRequest(paymentRequest))
			{
				this.ViewModel.NavigateTo(viewModel);
			}
		}
	}

	private class DeferredTopLevel(App app) : MainViewModel(app)
	{
		private TopLevel? topLevel;

		public override TopLevel? TopLevel
		{
			get => this.topLevel ??= TopLevel.GetTopLevel(this.TopVisual);
			set => throw new NotSupportedException();
		}

		internal Control? TopVisual { get; set; }
	}

	private class DesignTimePlatformServices : PlatformServices
	{
		public override bool IsOnACPower => false;

		public override bool IsNetworkMetered => false;

		public override bool IsRunningUnderTest => true;

		public override IDisposable? RequestSleepDeferral() => null;
	}
}
