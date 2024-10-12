// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;

namespace Nerdbank.Zcash.App.ViewModels;

public partial class LogsViewModel : ViewModelBase, IHasTitle
{
	private readonly IViewModelServices viewModelServices;

	private readonly ObservableAsPropertyHelper<IEnumerable<LogEvent>> messages;

	private string? selectedCategory;

	public LogsViewModel()
		: this(new DesignTimeViewModelServices())
	{
		ILogger syncLogger = this.viewModelServices.App.PlatformServices.LoggerFactory.CreateLogger("Sync");
		syncLogger.LogInformation("Some information");
		syncLogger.LogError("An error");

		ILogger otherLogger = this.viewModelServices.App.PlatformServices.LoggerFactory.CreateLogger("Other");
		otherLogger.LogError(new Exception("An exception message"), "an accompanying message");
	}

	public LogsViewModel(IViewModelServices viewModelServices)
	{
		this.viewModelServices = viewModelServices;

		this.SelectedCategory = this.Categories.FirstOrDefault();
		this.messages = this.WhenAnyValue(
			vm => vm.SelectedCategory,
			category => category is not null
				? (IEnumerable<LogEvent>?)this.viewModelServices.App.PlatformServices.MemoryLoggers?.GetLoggers(category).SelectMany(logger => logger.Messages).OrderBy(m => m.Timestamp) ?? []
				: []).ToProperty(this, nameof(this.Messages));
	}

	public string Title => LogsStrings.Title;

	public string CategoriesCaption => LogsStrings.CategoriesCaption;

	public ICollection<string> Categories => this.viewModelServices.App.PlatformServices.MemoryLoggers?.Categories ?? [];

	public string? SelectedCategory
	{
		get => this.selectedCategory;
		set => this.RaiseAndSetIfChanged(ref this.selectedCategory, value);
	}

	public IEnumerable<LogEvent> Messages => this.messages.Value;

	public string SeverityColumnCaption => LogsStrings.SeverityColumnCaption;

	public string MessageColumnCaption => LogsStrings.MessageColumnCaption;

	public string TimestampColumnCaption => LogsStrings.TimestampColumnCaption;
}
