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

	public Account(ZcashAccount account, HDWallet? memberOf)
	{
		this.ZcashAccount = account;
		this.MemberOf = memberOf;

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

	public HDWallet? MemberOf { get; }

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

	private class Formatter : IMessagePackFormatter<Account>
	{
		public Account Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
		{
			options.Security.DepthStep(ref reader);

			HDWallet? owner = ((AppSerializerOptions)options).HDWalletOwner;
			Zip32HDWallet? zip32 = null;
			string name = string.Empty;
			uint? accountIndex = null;
			UnifiedViewingKey? uvk = null;

			int length = reader.ReadArrayHeader();
			if (length < 1)
			{
				throw new MessagePackSerializationException("Invalid Account data.");
			}

			for (int i = 0; i < length; i++)
			{
				switch (i)
				{
					case 0:
						name = reader.ReadString() ?? string.Empty;
						break;
					case 1:
						if (owner is not null && reader.NextMessagePackType == MessagePackType.Integer)
						{
							accountIndex = reader.ReadUInt32();
							zip32 = owner.Zip32;
						}
						else if (owner is null && reader.NextMessagePackType == MessagePackType.Array)
						{
							if (reader.ReadArrayHeader() != 2)
							{
								throw new MessagePackSerializationException("Unexpected array length.");
							}

							accountIndex = reader.ReadUInt32();
							zip32 = options.Resolver.GetFormatterWithVerify<Zip32HDWallet>().Deserialize(ref reader, options);
						}
						else
						{
							uvk = UnifiedViewingKey.Decode(reader.ReadString()!);
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
				zcashAccount = new(uvk ?? throw new MessagePackSerializationException("Missing UVK."));
			}

			Account account = new(zcashAccount, owner)
			{
				Name = name,
			};

			return account;
		}

		public void Serialize(ref MessagePackWriter writer, Account value, MessagePackSerializerOptions options)
		{
			HDWallet? owner = ((AppSerializerOptions)options).HDWalletOwner;

			writer.WriteArrayHeader(2);
			writer.Write(value.Name);
			if (value.ZcashAccount.HDDerivation is not null)
			{
				if (owner is null)
				{
					writer.WriteArrayHeader(2);
					writer.Write(value.ZcashAccount.HDDerivation.Value.AccountIndex);
					options.Resolver.GetFormatterWithVerify<Zip32HDWallet>().Serialize(ref writer, value.ZcashAccount.HDDerivation.Value.Wallet, options);
				}
				else
				{
					writer.Write(value.ZcashAccount.HDDerivation.Value.AccountIndex);
				}
			}
			else
			{
				writer.Write(value.ZcashAccount.FullViewing?.UnifiedKey.TextEncoding ?? value.ZcashAccount.IncomingViewing.UnifiedKey.TextEncoding);
			}
		}
	}
}
