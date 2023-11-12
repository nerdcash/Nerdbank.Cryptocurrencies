// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using DynamicData.Binding;
using Microsoft.VisualStudio.Threading;
using Nerdbank.Cryptocurrencies.Exchanges;

namespace Nerdbank.Zcash.App.ViewModels;

public abstract class ViewModelBaseWithExchangeRate : ViewModelBaseWithAccountSelector
{
	private CancellationTokenSource? exchangeRateUpdateTokenSource;
	private bool exchangeRateTriggered;
	private ExchangeRate? exchangeRate;

	protected ViewModelBaseWithExchangeRate(IViewModelServices viewModelServices, bool showOnlyAccountsWithSpendKeys = false)
		: base(viewModelServices, showOnlyAccountsWithSpendKeys)
	{
	}

	public ExchangeRate? ExchangeRate
	{
		get
		{
			if (this.exchangeRateUpdateTokenSource is null or { IsCancellationRequested: true } && this.exchangeRate is null)
			{
				// Interest has been expressed, so start calculating the value.
				this.StartUpdateExchangeRate();
			}

			return this.exchangeRate;
		}

		set => this.RaiseAndSetIfChanged(ref this.exchangeRate, value);
	}

	protected async ValueTask UpdateExchangeRateAsync(CancellationToken cancellationToken)
	{
		this.exchangeRateTriggered = true;
		if (this.SelectedAccount is not null)
		{
			IReadOnlySet<Security> alternateSecurities = StableCoins.GetSecuritiesSharingPeg(this.ViewModelServices.Settings.AlternateCurrency);
			TradingPair? pair = await this.ViewModelServices.ExchangeRateProvider.FindFirstSupportedTradingPairAsync(
				this.SelectedAccount.Network.AsSecurity(),
				alternateSecurities,
				cancellationToken);
			if (pair is not null)
			{
				ExchangeRate rate = await this.ViewModelServices.ExchangeRateProvider.GetExchangeRateAsync(pair.Value, cancellationToken);

				// Only set this if the selected network still matches what we calculated.
				if (this.SelectedAccount.Network.AsSecurity() == rate.TradeInterest.Security)
				{
					this.ExchangeRate = rate;
				}

				return;
			}
		}

		this.ExchangeRate = null;
	}

	protected void StartUpdateExchangeRate()
	{
		this.exchangeRateUpdateTokenSource?.Cancel();
		this.exchangeRateUpdateTokenSource = new();
		this.UpdateExchangeRateAsync(this.exchangeRateUpdateTokenSource.Token).Forget();
	}

	protected override void OnSelectedAccountChanged()
	{
		if (this.exchangeRateTriggered && this.SelectedAccount?.Network.AsSecurity() != this.exchangeRate?.TradeInterest.Security)
		{
			this.StartUpdateExchangeRate();
		}
	}
}
