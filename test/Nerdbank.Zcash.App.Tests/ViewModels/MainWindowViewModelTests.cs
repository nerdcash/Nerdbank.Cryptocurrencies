// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Linq;

public class MainWindowViewModelTests : ViewModelTestBase
{
	private readonly MainWindowViewModel viewModel;

	public MainWindowViewModelTests()
	{
		this.viewModel = new MainWindowViewModel(this.CreateApp());
		this.MainViewModel = this.viewModel;
	}

	[Fact]
	public async Task HomeCommandStaysOnFirstLaunchAsync()
	{
		Assert.IsType<FirstLaunchViewModel>(this.viewModel.Content);
		await this.viewModel.HomeCommand.Execute().FirstAsync();
		Assert.IsType<FirstLaunchViewModel>(this.viewModel.Content);
		Assert.False(this.viewModel.CanNavigateBack);
	}

	[Fact]
	public async Task HomeCommandReturnsToFirstLaunchAsync()
	{
		Assert.IsType<FirstLaunchViewModel>(this.viewModel.Content);
		await this.viewModel.SettingsCommand.Execute().FirstAsync();
		Assert.IsType<SettingsViewModel>(this.viewModel.Content);
		await this.viewModel.HomeCommand.Execute().FirstAsync();
		Assert.IsType<FirstLaunchViewModel>(this.viewModel.Content);
		Assert.False(this.viewModel.CanNavigateBack);
	}

	[Fact]
	public async Task BackCommandReturnsToFirstLaunchAsync()
	{
		Assert.IsType<FirstLaunchViewModel>(this.viewModel.Content);
		await this.viewModel.SettingsCommand.Execute().FirstAsync();
		Assert.IsType<SettingsViewModel>(this.viewModel.Content);
		Assert.True(this.viewModel.CanNavigateBack);
		this.viewModel.NavigateBackCommand.Execute(null);
		Assert.IsType<FirstLaunchViewModel>(this.viewModel.Content);
		Assert.False(this.viewModel.CanNavigateBack);
	}
}
