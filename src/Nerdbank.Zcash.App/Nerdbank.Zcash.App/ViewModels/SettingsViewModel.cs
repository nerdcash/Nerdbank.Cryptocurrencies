// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Cryptocurrencies.Exchanges;

namespace Nerdbank.Zcash.App.ViewModels;

public class SettingsViewModel : ViewModelBase, IHasTitle
{
	private string lightServerUrlMainNet;
	private string lightServerUrlTestNet;
	private IViewModelServices viewModelServices;

	[Obsolete("For design-time use only", true)]
	public SettingsViewModel()
		: this(new DesignTimeViewModelServices())
	{
	}

	public SettingsViewModel(IViewModelServices viewModelServices)
	{
		this.viewModelServices = viewModelServices;

		this.lightServerUrlMainNet = this.viewModelServices.Settings.LightServerUrl.AbsoluteUri;
		this.lightServerUrlTestNet = this.viewModelServices.Settings.LightServerUrlTestNet.AbsoluteUri;
	}

	public string Title => "Settings";

	public Security AlternateCurrency
	{
		get => this.viewModelServices.Settings.AlternateCurrency;
		set
		{
			if (this.AlternateCurrency != value)
			{
				this.viewModelServices.Settings.AlternateCurrency = value;
				this.RaisePropertyChanged();
			}
		}
	}

	public string AlternateCurrencyCaption => "Preferred alternate currency";

	public string AlternateCurrencyExplanation => "The alternate currency is used to display the value of Zcash in a more familiar unit. The value of Zcash is volatile, so the value in your alternate currency may change rapidly.";

	public List<Security> AlternateCurrencies { get; } = Security.WellKnown.Values.ToList();

	public string AdvancedExpanderHeader => "Advanced";

	public string LightServerUrlMainNetCaption => "Light server URL (MainNet)";

	[Uri]
	public string LightServerUrlMainNet
	{
		get => this.lightServerUrlMainNet;
		set
		{
			this.RaiseAndSetIfChanged(ref this.lightServerUrlMainNet, value);
			if (value.Length > 0 && Uri.TryCreate(value, UriKind.Absolute, out Uri? result))
			{
				this.viewModelServices.Settings.LightServerUrl = result;
			}
		}
	}

	public string LightServerUrlTestNetCaption => "Light server URL (TestNet)";

	[Uri]
	public string LightServerUrlTestNet
	{
		get => this.lightServerUrlTestNet;
		set
		{
			this.RaiseAndSetIfChanged(ref this.lightServerUrlTestNet, value);
			if (value.Length > 0 && Uri.TryCreate(value, UriKind.Absolute, out Uri? result))
			{
				this.viewModelServices.Settings.LightServerUrl = result;
			}
		}
	}
}
