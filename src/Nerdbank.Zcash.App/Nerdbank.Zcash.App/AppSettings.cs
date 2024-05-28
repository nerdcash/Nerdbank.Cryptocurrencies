// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nerdbank.Cryptocurrencies;

namespace Nerdbank.Zcash.App;

public class AppSettings : IReactiveObject, ITopLevelPersistableData<AppSettings>
{
	private bool exchangeRatePerTransactionHasBeenDismissed;
	private Security? alternateCurrency = Security.USD;
	private bool downloadExchangeRates = true;
	private Uri lightServerUrl = new("https://zcash.mysideoftheweb.com:9067/");
	private Uri lightServerUrlTestNet = new("https://zcash.mysideoftheweb.com:19067/");
	private bool isDirty;
	private bool showProtocolDetails;

	public AppSettings()
	{
		this.MarkSelfDirtyOnPropertyChanged();
		this.SubscribePropertyChangedEvents();
		this.SubscribePropertyChangingEvents();
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	public event PropertyChangingEventHandler? PropertyChanging;

	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
	public bool ShowProtocolDetails
	{
		get => this.showProtocolDetails;
		set => this.RaiseAndSetIfChanged(ref this.showProtocolDetails, value);
	}

	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
	public bool ExchangeRatePerTransactionHasBeenDismissed
	{
		get => this.exchangeRatePerTransactionHasBeenDismissed;
		set => this.RaiseAndSetIfChanged(ref this.exchangeRatePerTransactionHasBeenDismissed, value);
	}

	[JsonIgnore]
	public Security? AlternateCurrency
	{
		get => this.alternateCurrency;
		set
		{
			if (this.alternateCurrency != value)
			{
				this.alternateCurrency = value;
				this.RaisePropertyChanged(nameof(this.AlternateCurrency));
				this.RaisePropertyChanged(nameof(this.AlternateCurrencySymbol));
			}
		}
	}

	public string? AlternateCurrencySymbol
	{
		get => this.alternateCurrency?.TickerSymbol;
		set => this.AlternateCurrency = value is not null ? Security.WellKnown[value] : null;
	}

	/// <summary>
	/// Gets or sets a value indicating whether exchange rates will be automatically downloaded.
	/// </summary>
	public bool DownloadExchangeRates
	{
		get => this.downloadExchangeRates;
		set => this.RaiseAndSetIfChanged(ref this.downloadExchangeRates, value);
	}

	public Uri LightServerUrl
	{
		get => this.lightServerUrl;
		set => this.RaiseAndSetIfChanged(ref this.lightServerUrl, value);
	}

	public Uri LightServerUrlTestNet
	{
		get => this.lightServerUrlTestNet;
		set => this.RaiseAndSetIfChanged(ref this.lightServerUrlTestNet, value);
	}

	[JsonIgnore]
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
