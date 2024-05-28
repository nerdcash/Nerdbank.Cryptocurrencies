// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.Threading;
using Nerdbank.Cryptocurrencies;

namespace Nerdbank.Zcash.App.ViewModels;

public class SettingsViewModel : ViewModelBase, IHasTitle
{
	private readonly Dictionary<ZcashNetwork, CancellationTokenSource> serverConfirmTokens = new();
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

		this.AlternateCurrencies = new ReadOnlyCollection<Security>(
			Security.WellKnown.Values
			.Where(s => s != Security.ZEC)
			.OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase).ToImmutableArray());
	}

	public string Title => SettingsStrings.Title;

	public Security? AlternateCurrency
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

	public string AlternateCurrencyCaption => SettingsStrings.AlternateCurrencyCaption;

	public string AlternateCurrencyExplanation => SettingsStrings.AlternateCurrencyExplanation;

	public ReadOnlyCollection<Security> AlternateCurrencies { get; }

	public string DownloadExchangeRatesCaption => SettingsStrings.DownloadExchangeRatesCaption;

	public bool DownloadExchangeRates
	{
		get => this.viewModelServices.Settings.DownloadExchangeRates;
		set
		{
			if (this.DownloadExchangeRates != value)
			{
				this.viewModelServices.Settings.DownloadExchangeRates = value;
				this.RaisePropertyChanged();
			}
		}
	}

	public string AdvancedExpanderHeader => SettingsStrings.AdvancedExpanderHeader;

	public string LightServerUrlMainNetCaption => SettingsStrings.LightServerUrlMainNetCaption;

	[Uri]
	public string LightServerUrlMainNet
	{
		get => this.lightServerUrlMainNet;
		set
		{
			this.RaiseAndSetIfChanged(ref this.lightServerUrlMainNet, value);
			Uri? result = null;
			if (value.Length > 0 && Uri.TryCreate(value, UriKind.Absolute, out result))
			{
				this.viewModelServices.Settings.LightServerUrl = result;
			}

			this.BeginConfirmServerNetwork(result, ZcashNetwork.MainNet);
		}
	}

	public string LightServerUrlTestNetCaption => SettingsStrings.LightServerUrlTestNetCaption;

	[Uri]
	public string LightServerUrlTestNet
	{
		get => this.lightServerUrlTestNet;
		set
		{
			this.RaiseAndSetIfChanged(ref this.lightServerUrlTestNet, value);
			Uri? result = null;
			if (value.Length > 0 && Uri.TryCreate(value, UriKind.Absolute, out result))
			{
				this.viewModelServices.Settings.LightServerUrlTestNet = result;
			}

			this.BeginConfirmServerNetwork(result, ZcashNetwork.TestNet);
		}
	}

	private void BeginConfirmServerNetwork(Uri? serverUrl, ZcashNetwork expectedNetwork, [CallerMemberName] string? propertyName = null)
	{
		Requires.NotNull(propertyName!, nameof(propertyName));

		// Cancel any previous confirmation.
		if (this.serverConfirmTokens.TryGetValue(expectedNetwork, out CancellationTokenSource? cts))
		{
			cts.Cancel();
			cts.Dispose();
		}

		if (serverUrl is null)
		{
			this.serverConfirmTokens.Remove(expectedNetwork);
			this.RecordValidationError(null, propertyName);
			return;
		}

		this.serverConfirmTokens[expectedNetwork] = cts = new();
		this.ConfirmServerNetworkAsync(serverUrl, propertyName, expectedNetwork, cts.Token).Forget();
	}

	private async ValueTask ConfirmServerNetworkAsync(Uri serverUrl, string propertyName, ZcashNetwork expectedNetwork, CancellationToken cancellationToken)
	{
		try
		{
			using ManagedLightWalletClient client = await ManagedLightWalletClient.CreateAsync(serverUrl, cancellationToken);
			if (expectedNetwork == client.Network)
			{
				this.RecordValidationError(null, propertyName);
			}
			else
			{
				this.RecordValidationError(SettingsStrings.FormatServerNetworkMismatch(client.Network), propertyName);
			}
		}
		catch (LightWalletException ex)
		{
			this.RecordValidationError(ex.Message, propertyName);
		}
	}
}
