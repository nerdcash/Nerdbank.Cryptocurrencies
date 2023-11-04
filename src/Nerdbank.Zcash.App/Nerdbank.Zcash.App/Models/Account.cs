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
	private readonly Guid identity;
	private string name = string.Empty;
	private decimal balance;
	private bool isDirty;

	public Account(ZcashAccount account, HDWallet? memberOf)
		: this(account, memberOf, Guid.NewGuid())
	{
	}

	private Account(ZcashAccount account, HDWallet? memberOf, Guid identity)
	{
		this.identity = identity;
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

			Account? account;
			Guid identity = Guid.Empty;

			AppSerializerOptions appOptions = (AppSerializerOptions)options;
			if (reader.NextMessagePackType != MessagePackType.Array)
			{
				// This is just a reference to an existing account.
				identity = options.Resolver.GetFormatterWithVerify<Guid>().Deserialize(ref reader, options);
				if (!appOptions.Accounts.TryGetValue(identity, out account))
				{
					throw new MessagePackSerializationException("Unknown account identity.");
				}

				return account;
			}

			HDWallet? owner = appOptions.HDWalletOwner;
			Zip32HDWallet? zip32 = null;
			string name = string.Empty;
			uint? accountIndex = null;
			UnifiedViewingKey? uvk = null;

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
						identity = options.Resolver.GetFormatterWithVerify<Guid>().Deserialize(ref reader, options);
						break;
					case 1:
						name = reader.ReadString() ?? string.Empty;
						break;
					case 2:
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

			account = new(zcashAccount, owner, identity)
			{
				Name = name,
			};

			appOptions.Accounts.Add(identity, account);
			return account;
		}

		public void Serialize(ref MessagePackWriter writer, Account value, MessagePackSerializerOptions options)
		{
			AppSerializerOptions appOptions = (AppSerializerOptions)options;
			if (appOptions.Accounts.ContainsKey(value.identity))
			{
				// This account has already been serialized elsewhere in the object graph. Just serialize a reference to it.
				options.Resolver.GetFormatterWithVerify<Guid>().Serialize(ref writer, value.identity, options);
				return;
			}

			HDWallet? owner = appOptions.HDWalletOwner;

			writer.WriteArrayHeader(3);
			options.Resolver.GetFormatterWithVerify<Guid>().Serialize(ref writer, value.identity, options);
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

			appOptions.Accounts.Add(value.identity, value);
		}
	}
}
