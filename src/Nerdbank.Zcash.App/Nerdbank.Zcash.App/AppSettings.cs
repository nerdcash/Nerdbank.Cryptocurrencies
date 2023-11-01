// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Text.Json;

namespace Nerdbank.Zcash.App;

public class AppSettings : IReactiveObject, ITopLevelPersistableData<AppSettings>
{
	private bool exchangeRatePerTransactionHasBeenDismissed;
	private Uri lightServerUrl = new("https://zcash.mysideoftheweb.com:9067/");
	private bool isDirty;

	public AppSettings()
	{
		this.MarkSelfDirtyOnPropertyChanged();
		this.SubscribePropertyChangedEvents();
		this.SubscribePropertyChangingEvents();
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

	public bool IsDirty
	{
		get => this.isDirty;
		set => this.RaiseAndSetIfChanged(ref this.isDirty, value);
	}

	public static AppSettings Load(Stream stream) => JsonSerializer.Deserialize(stream, JsonSourceGenerationContext.Default.AppSettings)!;

	public Task SaveAsync(Stream stream, CancellationToken cancellationToken) => JsonSerializer.SerializeAsync(stream, this, JsonSourceGenerationContext.Default.AppSettings, cancellationToken);

	void IReactiveObject.RaisePropertyChanging(PropertyChangingEventArgs args) => this.PropertyChanging?.Invoke(this, args);

	void IReactiveObject.RaisePropertyChanged(PropertyChangedEventArgs args) => this.PropertyChanged?.Invoke(this, args);
}
