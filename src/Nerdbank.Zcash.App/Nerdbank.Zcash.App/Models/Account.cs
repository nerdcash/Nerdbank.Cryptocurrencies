// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MessagePack;
using MessagePack.Formatters;
using Nerdbank.Cryptocurrencies.Exchanges;

namespace Nerdbank.Zcash.App.Models;

[MessagePackFormatter(typeof(Formatter))]
public class Account : ReactiveObject, IPersistableData
{
	private readonly ObservableAsPropertyHelper<SecurityAmount> securityBalance;
	private string name = string.Empty;
	private decimal balance;
	private bool isDirty;

	public Account(ZcashAccount account)
	{
		this.ZcashAccount = account;

		this.securityBalance = this.WhenAnyValue(
			vm => vm.Balance,
			balance => this.Network.AsSecurity().Amount(balance))
			.ToProperty(this, nameof(this.SecurityBalance));

		this.MarkSelfDirtyOnPropertyChanged();
	}

	public bool IsDirty
	{
		get => this.isDirty;
		set => this.RaiseAndSetIfChanged(ref this.isDirty, value);
	}

	public ZcashAccount ZcashAccount { get; }

	public ZcashNetwork Network => this.ZcashAccount.Network;

	public string Name
	{
		get => this.name;
		set => this.RaiseAndSetIfChanged(ref this.name, value);
	}

	public decimal Balance
	{
		get => this.balance;
		set => this.RaiseAndSetIfChanged(ref this.balance, value);
	}

	public SecurityAmount SecurityBalance => this.securityBalance.Value;

	private class Formatter : IMessagePackFormatter<Account?>
	{
		public Account? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
		{
			options.Security.DepthStep(ref reader);

			Zip32HDWallet? zip32 = null;
			string name = string.Empty;
			uint? accountIndex = null;
			string? uvk = null;

			int length = reader.ReadArrayHeader();
			if (length < 2)
			{
				throw new MessagePackSerializationException("Invalid Account data.");
			}

			for (int i = 0; i < length; i++)
			{
				switch (i)
				{
					case 0:
						name = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options) ?? string.Empty;
						break;
					case 1:
						uvk = options.Resolver.GetFormatterWithVerify<string>().Deserialize(ref reader, options);
						break;
					case 2:
						zip32 = options.Resolver.GetFormatterWithVerify<Zip32HDWallet>().Deserialize(ref reader, options);
						break;
					case 3:
						if (zip32 is null)
						{
							throw new MessagePackSerializationException();
						}

						if (!reader.TryReadNil())
						{
							accountIndex = reader.ReadUInt32();
						}

						break;
					default:
						reader.Skip();
						break;
				}
			}

			reader.Depth--;

			ZcashAccount zcashAccount;
			if (zip32 is not null)
			{
				zcashAccount = new(zip32, accountIndex ?? throw new MessagePackSerializationException("Missing account index."));
			}
			else
			{
				zcashAccount = new(UnifiedViewingKey.Decode(uvk ?? throw new MessagePackSerializationException("Missing UVK.")));
			}

			Account account = new(zcashAccount)
			{
				Name = name,
			};

			return account;
		}

		public void Serialize(ref MessagePackWriter writer, Account? value, MessagePackSerializerOptions options)
		{
			if (value is null)
			{
				writer.WriteNil();
				return;
			}

			writer.WriteArrayHeader(value.ZcashAccount.HDDerivation is null ? 2 : 4);

			options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.Name, options);

			if (value.ZcashAccount.HDDerivation is { } derivation)
			{
				writer.WriteNil();
				options.Resolver.GetFormatterWithVerify<Zip32HDWallet?>().Serialize(ref writer, derivation.Wallet, options);
				writer.Write(derivation.AccountIndex);
			}
			else
			{
				string v = value.ZcashAccount.FullViewing?.UnifiedKey.TextEncoding ?? value.ZcashAccount.IncomingViewing.UnifiedKey.TextEncoding;
				options.Resolver.GetFormatterWithVerify<string>().Serialize(ref writer, v, options);
			}
		}
	}
}
