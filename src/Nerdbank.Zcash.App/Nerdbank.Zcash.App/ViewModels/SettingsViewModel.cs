// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Cryptocurrencies.Exchanges;

namespace Nerdbank.Zcash.App.ViewModels;

public class SettingsViewModel : ViewModelBase, IHasTitle
{
	private Security alternateCurrency = Security.USD;
	private string lightServerUrl = "https://zcash.mysideoftheweb.com:9067/";

	public string Title => "Settings";

	public Security AlternateCurrency
	{
		get => this.alternateCurrency;
		set => this.RaiseAndSetIfChanged(ref this.alternateCurrency, value);
	}

	public string AlternateCurrencyCaption => "Preferred alternate currency";

	public string AlternateCurrencyExplanation => "The alternate currency is used to display the value of Zcash in a more familiar unit. The value of Zcash is volatile, so the value in your alternate currency may change rapidly.";

	public List<Security> AlternateCurrencies { get; } = Security.WellKnown.Values.ToList();

	public string AdvancedExpanderHeader => "Advanced";

	public string LightServerUrlCaption => "Light server URL";

	public string LightServerUrl
	{
		get => this.lightServerUrl;
		set => this.RaiseAndSetIfChanged(ref this.lightServerUrl, value);
	}
}
