// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using Nerdbank.Cryptocurrencies;
using Nerdbank.Cryptocurrencies.Exchanges;

namespace Nerdbank.Zcash.App.ViewModels;

public class DualAmountEntryViewModel : ViewModelBaseWithExchangeRate
{
	private readonly ObservableAsPropertyHelper<string> tickerSymbol;
	private readonly ObservableAsPropertyHelper<string?> alternateTickerSymbol;
	private readonly ObservableAsPropertyHelper<bool> isAlternateVisible;
	private decimal? amount;
	private string? amountText;
	private decimal? amountInAlternateCurrency;
	private string? amountInAlternateCurrencyText;

	[Obsolete("For design-time use only", error: true)]
	public DualAmountEntryViewModel()
		: this(new DesignTimeViewModelServices())
	{
		this.SelectedAccount = this.ViewModelServices.Wallet.Accounts.FirstOrDefault(a => a.Network == ZcashNetwork.MainNet);
	}

	public DualAmountEntryViewModel(IViewModelServices viewModelServices)
		: base(viewModelServices)
	{
		this.tickerSymbol = this.WhenAnyValue(
		vm => vm.SelectedAccount,
		a => a?.Network.GetTickerName() ?? UnknownSecurity.TickerSymbol).ToProperty(this, nameof(this.TickerSymbol));
		this.alternateTickerSymbol = this.WhenAnyValue<DualAmountEntryViewModel, string?, Security?>(
			vm => vm.ViewModelServices.Settings.AlternateCurrency,
			c => c?.TickerSymbol).ToProperty(this, nameof(this.AlternateTickerSymbol));

		bool amountPropagationInProgress = false;
		this.WhenAnyValue(
			vm => vm.Amount,
			vm => vm.SelectedAccount,
			vm => vm.ExchangeRate,
			(amount, account, rate) => ComputeAmountInAlternateCurrency(amount, account, rate))
			.Subscribe(v => UpdateAmountInAlternateCurrency(ref amountPropagationInProgress, () => this.AmountInAlternateCurrency = v?.RoundedAmount));
		this.WhenAnyValue<DualAmountEntryViewModel, SecurityAmount?, decimal?, Account?, ExchangeRate?>(
			vm => vm.AmountInAlternateCurrency,
			vm => vm.SelectedAccount,
			vm => vm.ExchangeRate,
			(amount, account, rate) => amount is not null && account is not null && rate is not null ? ComputeAmountInSelectedCurrency(amount.Value, account, rate.Value) : null)
			.Subscribe(v =>
			{
				if (v is not null)
				{
					UpdateAmountInAlternateCurrency(ref amountPropagationInProgress, () => this.Amount = v.Value.RoundedAmount);
				}
			});

		this.isAlternateVisible = this.WhenAnyValue<DualAmountEntryViewModel, bool, ExchangeRate?>(
			vm => vm.ExchangeRate,
			rate => rate is not null)
			.ToProperty(this, nameof(this.IsAlternateVisible));
	}

	public decimal? Amount
	{
		get => this.amount;
		set
		{
			this.RaiseAndSetIfChanged(ref this.amount, value);
			this.RaiseAndSetIfChanged(ref this.amountText, null, nameof(this.AmountText));
			this.RecordValidationError(null, nameof(this.AmountText));
		}
	}

	public string AmountText
	{
		get
		{
			if (this.amountText is null)
			{
				this.amountText = this.Amount?.ToString($"F{this.Security.Precision}") ?? string.Empty;
				this.RecordValidationError(null, nameof(this.AmountText));
			}

			return this.amountText;
		}

		set
		{
			this.RaiseAndSetIfChanged(ref this.amountText, value);
			if (decimal.TryParse(value, CultureInfo.CurrentCulture, out decimal parsed))
			{
				this.RecordValidationError(null, nameof(this.AmountText));
				this.RaiseAndSetIfChanged(ref this.amount, parsed, nameof(this.Amount));
			}
			else
			{
				this.RecordValidationError(TransactionStrings.InvalidValue, nameof(this.AmountText));
			}
		}
	}

	public string TickerSymbol => this.tickerSymbol.Value;

	public decimal? AmountInAlternateCurrency
	{
		get => this.amountInAlternateCurrency;
		set
		{
			this.RaiseAndSetIfChanged(ref this.amountInAlternateCurrency, value);
			this.RaiseAndSetIfChanged(ref this.amountInAlternateCurrencyText, null, nameof(this.AmountInAlternateCurrencyText));
			this.RecordValidationError(null, nameof(this.AmountInAlternateCurrencyText));
		}
	}

	public string AmountInAlternateCurrencyText
	{
		get
		{
			if (this.amountInAlternateCurrencyText is null)
			{
				this.amountInAlternateCurrencyText = this.AmountInAlternateCurrency?.ToString($"F{this.ExchangeRate?.Basis.Security.Precision}") ?? string.Empty;
				this.RecordValidationError(null, nameof(this.AmountInAlternateCurrencyText));
			}

			return this.amountInAlternateCurrencyText;
		}

		set
		{
			this.RaiseAndSetIfChanged(ref this.amountInAlternateCurrencyText, value);
			if (decimal.TryParse(value, CultureInfo.CurrentCulture, out decimal parsed))
			{
				this.RecordValidationError(null, nameof(this.AmountInAlternateCurrencyText));
				this.RaiseAndSetIfChanged(ref this.amountInAlternateCurrency, parsed, nameof(this.AmountInAlternateCurrency));
			}
			else
			{
				this.RecordValidationError(TransactionStrings.InvalidValue, nameof(this.AmountInAlternateCurrencyText));
			}
		}
	}

	public string? AlternateTickerSymbol => this.alternateTickerSymbol.Value;

	public bool IsAlternateVisible => this.isAlternateVisible.Value;

	private Security Security => this.SelectedAccount?.Network.AsSecurity() ?? UnknownSecurity;

	private static SecurityAmount? ComputeAmountInAlternateCurrency(decimal? amountInSelectedCurrency, Account? selectedAccount, ExchangeRate? exchangeRate)
		=> ConvertOrNull(exchangeRate, amountInSelectedCurrency is null ? null : selectedAccount?.Network.AsSecurity().Amount(amountInSelectedCurrency.Value));

	private static SecurityAmount ComputeAmountInSelectedCurrency(decimal amountInAlternateCurrency, Account selectedAccount, ExchangeRate exchangeRate)
		=> ConvertOrNull(exchangeRate, exchangeRate.Basis.Security.Amount(amountInAlternateCurrency))!.Value;

	private static void UpdateAmountInAlternateCurrency(ref bool propagating, Action update)
	{
		if (!propagating)
		{
			propagating = true;
			try
			{
				update();
			}
			finally
			{
				propagating = false;
			}
		}
	}

	private static SecurityAmount? ConvertOrNull(ExchangeRate? rate, SecurityAmount? amount)
		=> rate is not null && amount is not null && Describes(rate.Value, amount.Value.Security) ? amount * rate : null;

	/// <summary>
	/// Gets a value indicating whether this exchange rate describes a given security.
	/// </summary>
	/// <param name="rate">The exchange rate.</param>
	/// <param name="security">The security in question.</param>
	/// <returns><see langword="true"/> if either security described by this exchange rate is the given <paramref name="security"/>; otherwise <see langword="false" />.</returns>
	private static bool Describes(ExchangeRate rate, Security security) => rate.Basis.Security == security || rate.TradeInterest.Security == security;
}
