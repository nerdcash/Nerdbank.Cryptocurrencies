// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public class AppSettingsTests
{
	public AppSettingsTests(ITestOutputHelper logger)
	{
		this.Logger = logger;
	}

	public ITestOutputHelper Logger { get; }

	[Fact]
	public void SettingsCanChangeInMemory()
	{
		AppSettings settings = new();
		Assert.False(settings.ExchangeRatePerTransactionHasBeenDismissed);
		settings.ExchangeRatePerTransactionHasBeenDismissed = true;
		Assert.True(settings.ExchangeRatePerTransactionHasBeenDismissed);
	}

	[Fact]
	public async Task ChangedSettingsCanPersistAsync()
	{
		string jsonSettingsPath = Path.GetTempFileName();
		try
		{
			File.WriteAllText(jsonSettingsPath, @"{""showHints"":false, ""sampleProperty"":2}");

			AutoSaveManager<AppSettings> autoSaveManager = AutoSaveManager<AppSettings>.LoadOrCreate(jsonSettingsPath, enableAutoSave: false);
			AppSettings settings = autoSaveManager.Data;

			Assert.False(settings.ExchangeRatePerTransactionHasBeenDismissed);
			Assert.False(settings.IsDirty);

			settings.ExchangeRatePerTransactionHasBeenDismissed = true;
			Assert.True(settings.ExchangeRatePerTransactionHasBeenDismissed);
			Assert.True(settings.IsDirty);

			await autoSaveManager.SaveAsync(jsonSettingsPath, CancellationToken.None);
			Assert.False(settings.IsDirty);

			this.Logger.WriteLine(File.ReadAllText(jsonSettingsPath));
		}
		finally
		{
			File.Delete(jsonSettingsPath);
		}
	}

	[UIFact]
	public async Task AutoSave()
	{
		string jsonSettingsPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
		try
		{
			// The file does not yet exist, but this enables auto-save.
			var autoSaver = AutoSaveManager<AppSettings>.LoadOrCreate(jsonSettingsPath, enableAutoSave: true);
			AppSettings settings = autoSaver.Data;

			settings.ExchangeRatePerTransactionHasBeenDismissed = true;
			Assert.True(settings.IsDirty);

			// Wait for async save to finish.
			await autoSaver.DisposeAsync();
			this.Logger.WriteLine(File.ReadAllText(jsonSettingsPath));
			Assert.False(settings.IsDirty);

			settings = AutoSaveManager<AppSettings>.LoadOrCreate(jsonSettingsPath, enableAutoSave: true).Data;
			Assert.True(settings.ExchangeRatePerTransactionHasBeenDismissed);
		}
		finally
		{
			File.Delete(jsonSettingsPath);
		}
	}
}
