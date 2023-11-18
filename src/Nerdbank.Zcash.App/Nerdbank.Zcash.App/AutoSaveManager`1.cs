// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Diagnostics;
using System.Reactive.Concurrency;
using System.Text.Json;
using System.Threading.Tasks.Dataflow;
using Microsoft;

namespace Nerdbank.Zcash.App;

public class AutoSaveManager<T> : IAsyncDisposable
	where T : class, INotifyPropertyChanged, ITopLevelPersistableData<T>, new()
{
	private ActionBlock<bool>? saveOnceBlock;

	private AutoSaveManager(T data)
	{
		this.Data = data;
	}

	public T Data { get; }

	public static AutoSaveManager<T> LoadOrCreate(string path, bool enableAutoSave)
	{
		T? data = null;
		try
		{
			if (File.Exists(path))
			{
				using FileStream stream = File.OpenRead(path);
				data = T.Load(stream);
			}
		}
		catch (IOException)
		{
		}
		catch (JsonException)
		{
		}

		data ??= new();
		AutoSaveManager<T> result = new(data);
		if (enableAutoSave)
		{
			result.ConfigureAutoSave(path);
		}

		return result;
	}

	public Task SaveAsync(string path, CancellationToken cancellationToken)
	{
		Verify.Operation(this.saveOnceBlock is null, "Already auto-saving.");
		return this.SaveCoreAsync(path, cancellationToken);
	}

	public async ValueTask DisposeAsync()
	{
		if (this.saveOnceBlock is not null)
		{
			this.saveOnceBlock.Complete();
			await this.saveOnceBlock.Completion;
		}
	}

	private async Task SaveCoreAsync(string path, CancellationToken cancellationToken)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);

		using FileStream stream = new(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
		await this.Data.SaveAsync(stream, cancellationToken);
		this.Data.IsDirty = false;
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
				await this.SaveCoreAsync(tempFilePath, CancellationToken.None);
				File.Move(tempFilePath, autoSaveFilePath, overwrite: true);
			},
			new ExecutionDataflowBlockOptions
			{
				BoundedCapacity = 2,
				TaskScheduler = TaskScheduler.FromCurrentSynchronizationContext(),
			});

		this.Data.PropertyChanged += (sender, args) =>
		{
			if (sender is IPersistableData persistable)
			{
				if (persistable.IsDirty)
				{
					this.ScheduleSave();
				}
			}
			else
			{
				Debug.Fail("The sender is expected to be persistable.");
			}
		};
	}
}
