// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash.App.ViewModels;

public class MainWindowViewModel : MainViewModel
{
	private static readonly string AppTitle = Strings.AppTitle;
	private ObservableAsPropertyHelper<string> title;

	[Obsolete("Design-time only", error: true)]
	public MainWindowViewModel()
		: this(new App())
	{
	}

	public MainWindowViewModel(App app)
		: base(app)
	{
		this.title = this.WhenAnyValue<MainWindowViewModel, string, ViewModelBase?>(
			vm => vm.Content,
			content => content is IHasTitle titledViewModel ? $"{titledViewModel.Title} - {AppTitle}" : AppTitle)
			.ToProperty(this, nameof(this.Title));
	}

	public string Title => this.title.Value;
}
