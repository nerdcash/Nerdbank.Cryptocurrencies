// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Runtime.CompilerServices;
using MessagePack;
using MessagePack.Formatters;
using Nerdbank.Cryptocurrencies;
using Nerdbank.Cryptocurrencies.Exchanges;

namespace Nerdbank.Zcash.App.Models;

/// <summary>
/// Records exchange rates that have been discovered from historical records
/// or set specifically by the user for specific times that match transactions.
/// </summary>
[MessagePackFormatter(typeof(Formatter))]
public class ExchangeRateRecord : IPersistableDataHelper
{
	private readonly Dictionary<TradingPair, SortedDictionary<DateTimeOffset, ExchangeRate>> dataTables;

	[IgnoreMember]
	private bool isDirty = true;

	public ExchangeRateRecord()
		: this(CreateDataTables())
	{
	}

	private ExchangeRateRecord(Dictionary<TradingPair, SortedDictionary<DateTimeOffset, ExchangeRate>> dataTables)
	{
		this.dataTables = dataTables;
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	[IgnoreMember]
	public bool IsDirty
	{
		get => this.isDirty;
		set => this.SetIsDirty(ref this.isDirty, value);
	}

	/// <summary>
	/// Stores an exchange rate for a specific time.
	/// </summary>
	/// <param name="timestamp">The timestamp of the transaction whose exchange rate is to be stored.</param>
	/// <param name="rate">The exchange rate. The basis must be the alternate currency.</param>
	/// <remarks>
	/// If an exchange rate already exists for the given timestamp, it is overwritten.
	/// </remarks>
	public void SetExchangeRate(DateTimeOffset timestamp, ExchangeRate rate)
	{
		if (!this.dataTables.TryGetValue(rate.TradingPair, out SortedDictionary<DateTimeOffset, ExchangeRate>? table))
		{
			this.dataTables[rate.TradingPair] = table = new SortedDictionary<DateTimeOffset, ExchangeRate>();
		}

		table[timestamp] = rate;
		this.IsDirty = true;
	}

	/// <summary>
	/// Gets the exchange rate for a specific time.
	/// </summary>
	/// <param name="timestamp">The timestamp of the transaction whose exchange rate is to be retrieved.</param>
	/// <param name="tradingPair">The trading pair.</param>
	/// <param name="rate">Receives the exchange rate, if it is known for the given inputs.</param>
	/// <returns><see langword="true" /> if the exchange rate was available; otherwise <see langword="false" />.</returns>
	public bool TryGetExchangeRate(DateTimeOffset timestamp, TradingPair tradingPair, [NotNullWhen(true)] out ExchangeRate rate)
	{
		if (this.dataTables.TryGetValue(tradingPair, out SortedDictionary<DateTimeOffset, ExchangeRate>? subtable))
		{
			if (subtable.TryGetValue(timestamp, out rate))
			{
				return true;
			}
		}

		rate = default;
		return false;
	}

	/// <summary>
	/// Retrieves the exchange rate for a specific time, falling back to a historical exchange rate provider if necessary.
	/// </summary>
	/// <param name="provider">The provider of the historical exchange rates.</param>
	/// <param name="timestamp">The timestamp on the transaction.</param>
	/// <param name="tradingPair">The trading pair, where the alternate currency fills the Basis slot.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>
	/// The exchange rate, or <see langword="null" /> if it wasn't already available for this transaction
	/// and wasn't available for retrieval from the historical exchange rate provider.
	/// </returns>
	public async ValueTask<ExchangeRate?> GetExchangeRateAsync(IHistoricalExchangeRateProvider provider, DateTimeOffset timestamp, TradingPair tradingPair, CancellationToken cancellationToken)
	{
		if (this.TryGetExchangeRate(timestamp, tradingPair, out ExchangeRate rate))
		{
			return rate;
		}

		IReadOnlySet<Security> alternateSecurities = StableCoins.GetSecuritiesSharingPeg(tradingPair.Basis);
		TradingPair? tradingPairWithInfo = await provider.FindFirstSupportedTradingPairAsync(tradingPair.TradeInterest, alternateSecurities, cancellationToken);
		if (tradingPairWithInfo is null)
		{
			return null;
		}

		rate = await provider.GetExchangeRateAsync(tradingPairWithInfo.Value, timestamp, cancellationToken);
		this.SetExchangeRate(timestamp, rate);
		return rate;
	}

	void IPersistableDataHelper.OnPropertyChanged(string propertyName) => this.OnPropertyChanged(propertyName);

	void IPersistableDataHelper.ClearDirtyFlagOnMembers()
	{
	}

	protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) => this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

	private static Dictionary<TradingPair, SortedDictionary<DateTimeOffset, ExchangeRate>> CreateDataTables() => new(TradingPairEitherOrderEqualityComparer.Instance);

	private class Formatter : IMessagePackFormatter<ExchangeRateRecord>
	{
		public static readonly Formatter Instance = new();

		private Formatter()
		{
		}

		public ExchangeRateRecord Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
		{
			IMessagePackFormatter<TradingPair> tradingPairFormatter = options.Resolver.GetFormatterWithVerify<TradingPair>();
			IMessagePackFormatter<DateTimeOffset> dateTimeFormatter = options.Resolver.GetFormatterWithVerify<DateTimeOffset>();
			IMessagePackFormatter<decimal> exchangeRateFormatter = options.Resolver.GetFormatterWithVerify<decimal>();

			options.Security.DepthStep(ref reader);

			Dictionary<TradingPair, SortedDictionary<DateTimeOffset, ExchangeRate>> dataTables = CreateDataTables();

			int outerCount = reader.ReadMapHeader();
			for (int i = 0; i < outerCount; i++)
			{
				TradingPair tradingPair = tradingPairFormatter.Deserialize(ref reader, options);
				SortedDictionary<DateTimeOffset, ExchangeRate> subtable = new();
				dataTables[tradingPair] = subtable;

				int innerCount = reader.ReadMapHeader();
				for (int j = 0; j < innerCount; j++)
				{
					DateTimeOffset dateTime = dateTimeFormatter.Deserialize(ref reader, options);

					decimal ratio = exchangeRateFormatter.Deserialize(ref reader, options);
					ExchangeRate rate = new(tradingPair.Basis.Amount(ratio), tradingPair.TradeInterest.Amount(1));

					subtable.Add(dateTime, rate);
				}
			}

			reader.Depth--;

			ExchangeRateRecord result = new(dataTables);
			result.isDirty = false;
			return result;
		}

		public void Serialize(ref MessagePackWriter writer, ExchangeRateRecord value, MessagePackSerializerOptions options)
		{
			IMessagePackFormatter<TradingPair> tradingPairFormatter = options.Resolver.GetFormatterWithVerify<TradingPair>();
			IMessagePackFormatter<DateTimeOffset> dateTimeFormatter = options.Resolver.GetFormatterWithVerify<DateTimeOffset>();
			IMessagePackFormatter<decimal> exchangeRateFormatter = options.Resolver.GetFormatterWithVerify<decimal>();

			writer.WriteMapHeader(value.dataTables.Count);
			foreach ((TradingPair tradingPair, SortedDictionary<DateTimeOffset, ExchangeRate> subtable) in value.dataTables)
			{
				tradingPairFormatter.Serialize(ref writer, tradingPair, options);

				writer.WriteMapHeader(subtable.Count);
				foreach ((DateTimeOffset dateTime, ExchangeRate rate) in subtable)
				{
					dateTimeFormatter.Serialize(ref writer, dateTime, options);

					// To avoid storing redundant data, instead of storing the full ExchangeRate value,
					// just store the ratio.
					exchangeRateFormatter.Serialize(ref writer, rate.InBasisAmount.Amount, options);
				}
			}
		}
	}
}
