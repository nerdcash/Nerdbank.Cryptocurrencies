// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Models;

public class DataRootTests : ModelTestBase<DataRoot>
{
	public DataRootTests(ITestOutputHelper logger)
		: base(logger)
	{
	}

	public DataRoot DataRoot { get; set; } = new();

	public override DataRoot Model => this.DataRoot;

	[UIFact]
	public async Task AutoSave()
	{
		string dataPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
		try
		{
			// The file does not yet exist, but this enables auto-save.
			var autoSaver = AutoSaveManager<DataRoot>.LoadOrCreate(dataPath, enableAutoSave: true);
			DataRoot data = autoSaver.Data;

			data.ContactManager.Add(new Contact { Name = "Andrew" });
			Assert.True(data.IsDirty);

			// Wait for async save to finish.
			await autoSaver.DisposeAsync();
			Assert.False(data.IsDirty);

			data = AutoSaveManager<DataRoot>.LoadOrCreate(dataPath, enableAutoSave: true).Data;
			Assert.Equal("Andrew", Assert.Single(data.ContactManager.Contacts).Name);
		}
		finally
		{
			File.Delete(dataPath);
		}
	}
}
