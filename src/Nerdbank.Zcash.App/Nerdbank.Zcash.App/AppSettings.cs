// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Text.Json;
using System.Threading.Tasks.Dataflow;
using Microsoft;

namespace Nerdbank.Zcash.App;

public class AppSettings : IAsyncDisposable, IReactiveObject
{
	private ActionBlock<bool>? saveOnceBlock;
	private bool exchangeRatePerTransactionHasBeenDismissed;
	private Uri lightServerUrl = new("https://zcash.mysideoftheweb.com:9067/");

	public AppSettings()
	{
		this.SubscribePropertyChangedEvents();
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	public event PropertyChangingEventHandler? PropertyChanging;

	public bool ExchangeRatePerTransactionHasBeenDismissed
	{
		get => this.exchangeRatePerTransactionHasBeenDismissed;
		set => this.RaiseAndSetIfChanged(ref this.exchangeRatePerTransactionHasBeenDismissed, value);
	}

	public Uri LightServerUrl
	{
		get => this.lightServerUrl;
		set => this.RaiseAndSetIfChanged(ref this.lightServerUrl, value);
	}

	public static AppSettings LoadOrCreate(string jsonSettingsPath, bool enableAutoSave)
	{
		AppSettings? result = null;
		try
		{
			if (File.Exists(jsonSettingsPath))
			{
				using FileStream stream = File.OpenRead(jsonSettingsPath);
				result = JsonSerializer.Deserialize(stream, JsonSourceGenerationContext.Default.AppSettings);
			}
		}
		catch (IOException)
		{
		}
		catch (JsonException)
		{
		}

		result ??= new();
		if (enableAutoSave)
		{
			result.ConfigureAutoSave(jsonSettingsPath);
		}

		return result;
	}

	public async Task SaveAsync(string jsonSettingsPath, CancellationToken cancellationToken)
	{
		using FileStream stream = new(jsonSettingsPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
		await JsonSerializer.SerializeAsync(stream, this, JsonSourceGenerationContext.Default.AppSettings, cancellationToken);
	}

	public async ValueTask DisposeAsync()
	{
		if (this.saveOnceBlock is not null)
		{
			this.saveOnceBlock.Complete();
			await this.saveOnceBlock.Completion;
		}
	}

	void IReactiveObject.RaisePropertyChanging(PropertyChangingEventArgs args)
	{
		this.PropertyChanging?.Invoke(this, args);
	}

	void IReactiveObject.RaisePropertyChanged(PropertyChangedEventArgs args)
	{
		this.PropertyChanged?.Invoke(this, args);
	}

	private void ScheduleSave()
	{
		this.saveOnceBlock?.Post(true);
	}

	private void ConfigureAutoSave(string autoSaveFilePath)
	{
		Verify.Operation(this.saveOnceBlock is null, "Already auto-saving.");

		// We arrange for async saves to happen with an action block that will never schedule more than one save beyond
		// whatever async save may already be in progress. Anything more than that would be wasteful.
		this.saveOnceBlock = new(
			async _ =>
			{
				// Save to a temporary file first, then move it into place.
				// This ensures that a crash during save doesn't corrupt the file.
				string tempFilePath = $"{autoSaveFilePath}.new";
				await this.SaveAsync(tempFilePath, CancellationToken.None);
				File.Move(tempFilePath, autoSaveFilePath, overwrite: true);
			},
			new ExecutionDataflowBlockOptions { BoundedCapacity = 2 });

		this.PropertyChanged += (_, _) => this.ScheduleSave();
	}
}
