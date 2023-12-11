// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Reactive.Linq;
using Mocks;

namespace ViewModels;

public abstract class ViewModelTestBase : IAsyncLifetime
{
	protected static readonly TimeSpan UnexpectedTimeout = Debugger.IsAttached ? Timeout.InfiniteTimeSpan : TimeSpan.FromSeconds(15);
	private readonly string? testSandboxPath;

	public ViewModelTestBase()
		: this(persistStateAcrossReinitializations: false)
	{
	}

	public ViewModelTestBase(bool persistStateAcrossReinitializations)
	{
		if (persistStateAcrossReinitializations)
		{
			this.testSandboxPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
			this.PersistedPaths = (Path.Combine(this.testSandboxPath, "settings"), Path.Combine(this.testSandboxPath, "data"));
		}

		this.MainViewModel = new MainViewModel(this.CreateApp())
		{
			ExchangeRateProvider = new MockExchangeRateProvider(),
			HistoricalExchangeRateProvider = new MockExchangeRateProvider(),
		};
	}

	protected CancellationToken TimeoutToken { get; } = new CancellationTokenSource(UnexpectedTimeout).Token;

	protected (string Settings, string Data)? PersistedPaths { get; }

	protected App App => this.MainViewModel.App;

	protected MainViewModel MainViewModel { get; set; }

	public virtual Task InitializeAsync()
	{
		return Task.CompletedTask;
	}

	public virtual async Task DisposeAsync()
	{
		await this.App.DisposeAsync();

		// Delete the temporary directory we created earlier.
		if (this.testSandboxPath is not null)
		{
			Directory.Delete(this.testSandboxPath, recursive: true);
		}
	}

	protected static bool ValidateResults(ViewModelBase viewModel, out IReadOnlyList<ValidationResult> errors)
	{
		List<ValidationResult> validationResults = new();
		ValidationContext context = new(viewModel, serviceProvider: null, items: null);
		bool isValid = Validator.TryValidateObject(viewModel, context, validationResults, true);
		errors = validationResults;
		return isValid;
	}

	protected static Task WatchForAsync<T>(T owner, string propertyName, Func<bool> tester, CancellationToken cancellationToken)
		where T : INotifyPropertyChanged
	{
		if (tester())
		{
			return Task.CompletedTask;
		}

		CancellationTokenRegistration ctr = default;

		TaskCompletionSource<bool> tcs = new();
		owner.PropertyChanged += OnPropertyChange;

		ctr = cancellationToken.Register(() =>
		{
			owner.PropertyChanged -= OnPropertyChange;
			tcs.TrySetCanceled(cancellationToken);
		});

		return tcs.Task;

		void OnPropertyChange(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == propertyName && tester())
			{
				owner.PropertyChanged -= OnPropertyChange;
				tcs.TrySetResult(true);
				ctr.Dispose();
			}
		}
	}

	protected App CreateApp()
	{
		AppPlatformSettings appPlatformSettings = new AppPlatformSettings
		{
			ConfidentialDataPath = this.PersistedPaths?.Data,
			NonConfidentialDataPath = this.PersistedPaths?.Settings,
		};

		App app = new(appPlatformSettings, new MockPlatformServices());
		app.InitializeFields();
		return app;
	}

	protected async Task ReinitializeAppAsync()
	{
		await this.MainViewModel.App.DisposeAsync();
		this.MainViewModel = new MainViewModel(this.CreateApp());
	}

	protected async Task InitializeWalletAsync()
	{
		FirstLaunchViewModel firstLaunch = new(this.MainViewModel);
		await firstLaunch.StartNewWalletCommand.Execute().FirstAsync();
	}
}
