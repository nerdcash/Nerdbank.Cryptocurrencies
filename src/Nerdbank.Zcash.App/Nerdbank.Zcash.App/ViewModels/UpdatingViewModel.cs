// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Linq;
using Velopack;

namespace Nerdbank.Zcash.App.ViewModels;

public class UpdatingViewModel : ViewModelBase
{
	private readonly ObservableAsPropertyHelper<string?> newerVersion;
	private readonly ObservableAsPropertyHelper<string> updateAndRestartCommandCaption;
	private readonly ObservableAsPropertyHelper<bool> isUpdateAndRestartCommandVisible;

	public UpdatingViewModel(AppUpdateManager updateManager)
	{
		this.Model = updateManager;
		this.UpdateAndRestartCommand = ReactiveCommand.CreateFromTask(updateManager.RestartUpdatedVersionAsync, updateManager.IsUpdateReady);
		this.newerVersion = updateManager.UpdateInfo
			.Select(ui => ui?.TargetFullRelease.Version.ToString())
			.ToProperty(this, nameof(this.NewerVersion));
		this.updateAndRestartCommandCaption = this.WhenAnyValue(x => x.NewerVersion)
			.Select(v => $"Update to {v}")
			.ToProperty(this, nameof(this.UpdateAndRestartCommandCaption));
		this.isUpdateAndRestartCommandVisible = updateManager.IsUpdateReady.ToProperty(this, nameof(this.IsUpdateAndRestartCommandVisible));
	}

	public AppUpdateManager Model { get; }

	public SelfUpdateProgressData UpdateDownloading => this.Model.UpdateDownloading;

	public ReactiveCommand<Unit, Unit> UpdateAndRestartCommand { get; }

	public string UpdateAndRestartCommandCaption => this.updateAndRestartCommandCaption.Value;

	public bool IsUpdateAndRestartCommandVisible => this.isUpdateAndRestartCommandVisible.Value;

	public string? NewerVersion => this.newerVersion.Value;
}
