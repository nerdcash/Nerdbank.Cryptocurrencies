// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Cryptocurrencies.Exchanges;

namespace Nerdbank.Zcash.App;

public interface IAppServices
{
	AppPlatformSettings AppPlatformSettings { get; }

	AppSettings Settings { get; }

	/// <summary>
	/// Gets the wallet data model.
	/// </summary>
	ZcashWallet Wallet { get; }

	/// <summary>
	/// Gets the persisted collection of contacts.
	/// </summary>
	IContactManager ContactManager { get; }

	/// <summary>
	/// Gets the record of exchange rates that go with actual transactions.
	/// </summary>
	ExchangeRateRecord ExchangeData { get; }

	/// <summary>
	/// Gets a provider of exchange rates.
	/// </summary>
	IExchangeRateProvider ExchangeRateProvider { get; }

	/// <summary>
	/// Gets a provider of historical exchange rates.
	/// </summary>
	IHistoricalExchangeRateProvider HistoricalExchangeRateProvider { get; }

	/// <summary>
	/// Tracks a transaction send operation, to ensure the process does not exit before it has completed broadcasting.
	/// </summary>
	/// <param name="sendTask">The task representing the operation.</param>
	void RegisterSendTransactionTask(Task sendTask);
}
