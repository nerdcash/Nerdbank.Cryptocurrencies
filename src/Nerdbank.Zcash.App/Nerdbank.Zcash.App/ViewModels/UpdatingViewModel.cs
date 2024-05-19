// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Linq;
using Velopack;

namespace Nerdbank.Zcash.App.ViewModels;

public class UpdatingViewModel : ViewModelBase
{
	private readonly ObservableBox<bool> isUpdateReady = new(false);
	private readonly UpdateManager? updateManager;
	private readonly ObservableAsPropertyHelper<string?> newerVersion;
	private readonly ObservableAsPropertyHelper<string> updateAndRestartCommandCaption;
	private readonly ObservableAsPropertyHelper<bool> isUpdateAndRestartCommandVisible;
	private readonly App app;
	private UpdateInfo? updateInfo;

	public UpdatingViewModel(App app, string? velopackUpdateUrl)
	{
		this.app = app;
		if (velopackUpdateUrl is not null)
		{
			this.updateManager = new(velopackUpdateUrl);
		}

		this.UpdateAndRestartCommand = ReactiveCommand.CreateFromTask(this.RestartUpdatedVersionAsync, this.isUpdateReady);
		this.newerVersion = this.WhenAnyValue(x => x.UpdateInfo)
			.Select(ui => ui?.TargetFullRelease.Version.ToString())
			.ToProperty(this, nameof(this.NewerVersion));
		this.updateAndRestartCommandCaption = this.WhenAnyValue(x => x.NewerVersion)
			.Select(v => $"Update to {v}")
			.ToProperty(this, nameof(this.UpdateAndRestartCommandCaption));
		this.isUpdateAndRestartCommandVisible = this.isUpdateReady.ToProperty(this, nameof(this.IsUpdateAndRestartCommandVisible));
	}

	public ReactiveCommand<Unit, Unit> UpdateAndRestartCommand { get; }

	public string UpdateAndRestartCommandCaption => this.updateAndRestartCommandCaption.Value;

	public bool IsUpdateAndRestartCommandVisible => this.isUpdateAndRestartCommandVisible.Value;

	public SelfUpdateProgressData UpdateDownloading { get; } = new();

	public string? NewerVersion => this.newerVersion.Value;

	private UpdateInfo? UpdateInfo
	{
		get => this.updateInfo;
		set => this.RaiseAndSetIfChanged(ref this.updateInfo, value);
	}

	public async Task RestartUpdatedVersionAsync()
	{
		Verify.Operation(this.updateManager is not null, "Channel URL must be specified on construction.");
		Verify.Operation(this.UpdateInfo is not null, "No update available.");

		// Conduct a graceful exit of the app.
		await this.app.DisposeAsync();

		// This must be done last, as it exits the application.
		this.updateManager.ApplyUpdatesAndRestart(this.UpdateInfo);
	}

	internal async Task UpdateMyAppAsync(CancellationToken cancellationToken)
	{
		Verify.Operation(this.updateManager is not null, "Channel URL must be specified on construction.");
		this.isUpdateReady.Value = this.updateManager.IsUpdatePendingRestart;

		if (!this.updateManager.IsInstalled)
		{
			// Skip updating an app that isn't in an installed configuration.
			return;
		}

		while (!cancellationToken.IsCancellationRequested)
		{
			// check for new version
			this.UpdateInfo = await this.updateManager.CheckForUpdatesAsync();
			if (this.UpdateInfo is not null)
			{
				// Download new version
				this.UpdateDownloading.NotifyDownloadingUpdate(this.UpdateInfo.TargetFullRelease.Version.ToString());

				await this.updateManager.DownloadUpdatesAsync(
					this.UpdateInfo,
					percent => this.UpdateDownloading.Current = (ulong)percent,
					cancelToken: cancellationToken);

				this.UpdateDownloading.Complete();
				this.isUpdateReady.Value = true;
			}

			// This app has a tendency to be left open for days at a time.
			// So rather than only check for updates on startup, we'll check once a day.
			await Task.Delay(TimeSpan.FromDays(1), cancellationToken);
		}
	}

	internal async Task MockUpdateAsync(CancellationToken cancellationToken)
	{
		await Task.Delay(2000, cancellationToken);
		this.UpdateInfo = new(new VelopackAsset { Version = new(1, 2, 3) }, false);
		this.UpdateDownloading.NotifyDownloadingUpdate("Mock");
		for (int i = 0; i <= 10; i++)
		{
			this.UpdateDownloading.Current = (ulong)(i * 10);
			await Task.Delay(1000, cancellationToken);
		}

		this.UpdateDownloading.Complete();
		this.isUpdateReady.Value = true;
	}
}
