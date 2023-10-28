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

			AppSettings settings = AppSettings.LoadOrCreate(jsonSettingsPath, enableAutoSave: false);
			Assert.False(settings.ExchangeRatePerTransactionHasBeenDismissed);
			settings.ExchangeRatePerTransactionHasBeenDismissed = true;
			Assert.True(settings.ExchangeRatePerTransactionHasBeenDismissed);
			await settings.SaveAsync(jsonSettingsPath, CancellationToken.None);

			this.Logger.WriteLine(File.ReadAllText(jsonSettingsPath));
		}
		finally
		{
			File.Delete(jsonSettingsPath);
		}
	}

	[Fact]
	public async Task AutoSave()
	{
		string jsonSettingsPath = Path.GetTempFileName();
		try
		{
			// The file does not yet exist, but this enables auto-save.
			AppSettings settings = AppSettings.LoadOrCreate(jsonSettingsPath, enableAutoSave: true);

			settings.ExchangeRatePerTransactionHasBeenDismissed = true;

			// Wait for async save to finish.
			await settings.DisposeAsync();
			this.Logger.WriteLine(File.ReadAllText(jsonSettingsPath));

			settings = AppSettings.LoadOrCreate(jsonSettingsPath, enableAutoSave: true);
			Assert.True(settings.ExchangeRatePerTransactionHasBeenDismissed);
		}
		finally
		{
			File.Delete(jsonSettingsPath);
		}
	}
}
