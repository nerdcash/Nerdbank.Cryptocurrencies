// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Nerdbank.Cryptocurrencies;
using Nerdbank.Cryptocurrencies.Exchanges;

namespace Nerdbank.Zcash.App;

internal class DesignTimeViewModelServices : IViewModelServices
{
	internal DesignTimeViewModelServices(bool empty = false)
	{
		if (!empty)
		{
			// Populate accounts.
			Bip39Mnemonic mnemonic = Bip39Mnemonic.Create(Zip32HDWallet.MinimumEntropyLengthInBits);
			Zip32HDWallet mainNet = new(mnemonic, ZcashNetwork.MainNet);
			Zip32HDWallet testNet = new(mnemonic, ZcashNetwork.TestNet);

			Account playMoneyAccount = NewAccount(testNet, 0, Strings.FormatDefaultNameForFirstAccountWithTicker(ZcashNetwork.TestNet), 1.23m);
			Account realAccount = NewAccount(mainNet, 0, "Real ZEC", 0.023m);
			Account savingsAccount = NewAccount(mainNet, 1, "Savings", 3.45m);

			Account NewAccount(Zip32HDWallet zip32, uint index, string name, decimal spendableBalance)
			{
				return new(new ZcashAccount(zip32, index) { BirthdayHeight = 123456 })
				{
					Name = name,
					Balance = new()
					{
						Spendable = zip32.Network.AsSecurity().Amount(spendableBalance),
					},
				};
			}

			this.Wallet.Add(playMoneyAccount);
			this.Wallet.Add(savingsAccount);
			this.Wallet.Add(realAccount);

			// Populate address book.
			this.ContactManager.Add(new Contact { Name = "Andrew Arnott", ReceivingAddresses = { ZcashAddress.Decode("t1a7w3qM23i4ajQcbX5wd6oH4zTY8Bry5vF") } });
			this.ContactManager.Add(new Contact { Name = "Jason Arnott", ReceivingAddresses = { ZcashAddress.Decode("u17kydrnuh9k8dqtud9qugel5ym835xqg8jk5czy2qcxea0zucru7d9w0c9hcq43898l2d993taaqh6vr0u6yskjnn582vyvu8qqk6qyme0z2vfgcclxatca7cx2f45v2n9zfd7hmkwlrw0wt38z9ua2yvgdnvppucyf2cfsxwlyfy339k") } });
			this.ContactManager.Add(new Contact { Name = "David Arnott" });
		}
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	[Obsolete("Design-time only.")] // necessary to avoid the compile error about App() being obsolete
	public App App { get; } = new();

	public AppPlatformSettings AppPlatformSettings { get; } = App.CreateDesignTimeAppPlatformSettings();

	public ZcashWallet Wallet { get; } = new();

	public Account? MostRecentlyUsedAccount { get; set; }

	public IContactManager ContactManager { get; } = new DesignTimeContactManager();

	public TopLevel? TopLevel => null;

	public AppSettings Settings { get; } = new();

	public ExchangeRateRecord ExchangeData { get; } = new();

	public IExchangeRateProvider ExchangeRateProvider { get; } = new MockExchange();

	public IHistoricalExchangeRateProvider HistoricalExchangeRateProvider { get; } = new MockExchange();

	public bool IsScanCommandAvailable => true;

	public void RegisterSendTransactionTask(Task sendTask)
	{
	}

	public void NavigateBack(ViewModelBase? ifCurrentViewModel)
	{
	}

	public T NavigateTo<T>(T viewModel)
		where T : ViewModelBase
	{
		return viewModel;
	}

	public T ReplaceViewStack<T>(T viewModel)
		where T : ViewModelBase
	{
		return viewModel;
	}

	protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	private class DesignTimeContactManager : IContactManager
	{
		private ObservableCollection<Contact> contacts = new();

		public DesignTimeContactManager()
		{
			this.Contacts = new(this.contacts);
		}

		public ReadOnlyObservableCollection<Contact> Contacts { get; }

		public void Add(Contact contact) => this.contacts.Add(contact);

		public bool Remove(Contact contact) => this.Remove(contact);
	}

	private class MockExchange : IExchangeRateProvider, IHistoricalExchangeRateProvider
	{
		public ValueTask<IReadOnlySet<TradingPair>> GetAvailableTradingPairsAsync(CancellationToken cancellationToken)
		{
			return new ValueTask<IReadOnlySet<TradingPair>>(ImmutableHashSet.Create(new TradingPair(Security.USD, Security.ZEC)));
		}

		public ValueTask<ExchangeRate?> GetExchangeRateAsync(TradingPair tradingPair, DateTimeOffset when, CancellationToken cancellationToken)
		{
			return new(new ExchangeRate(tradingPair.Basis.Amount(10 + (when.Day * 2)), tradingPair.TradeInterest.Amount(1)));
		}

		public ValueTask<ExchangeRate> GetExchangeRateAsync(TradingPair tradingPair, CancellationToken cancellationToken)
		{
			return new(new ExchangeRate(tradingPair.Basis.Amount(30), tradingPair.TradeInterest.Amount(1)));
		}
	}
}
