// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Runtime.CompilerServices;
using MessagePack;
using MessagePack.Formatters;
using Microsoft;
using Nerdbank.Cryptocurrencies;

namespace Nerdbank.Zcash.App.Models;

/// <summary>
/// A wallet for all the accounts created from a single seed phrase.
/// </summary>
[MessagePackFormatter(typeof(Formatter))]
public class HDWallet : IPersistableDataHelper
{
	private readonly SortedDictionary<uint, Account> accounts = new();
	private bool isSeedPhraseBackedUp;
	private string name = string.Empty;
	private bool isDirty;

	public HDWallet(Zip32HDWallet zip32)
	{
		this.Zip32 = zip32;
		this.MarkSelfDirtyOnPropertyChanged();
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	public bool IsDirty
	{
		get => this.isDirty;
		set => this.SetIsDirty(ref this.isDirty, value);
	}

	/// <summary>
	/// Gets or sets an optional name for an HD wallet.
	/// </summary>
	/// <remarks>
	/// HD wallets should have names when there are more than one of them so they can be grouped together in the UI
	/// and the user can understand the groupings.
	/// </remarks>
	public string Name
	{
		get => this.name;
		set => this.RaiseAndSetIfChanged(ref this.name, value);
	}

	public bool IsSeedPhraseBackedUp
	{
		get => this.isSeedPhraseBackedUp;
		set => this.RaiseAndSetIfChanged(ref this.isSeedPhraseBackedUp, value);
	}

	public Zip32HDWallet Zip32 { get; }

	/// <summary>
	/// Gets or sets the birthday height for the overall HD wallet.
	/// </summary>
	public ulong BirthdayHeight { get; set; }

	public IReadOnlyDictionary<uint, Account> Accounts => this.accounts;

	public uint MaxAccountIndex => this.Accounts.Count > 0 ? this.Accounts.Keys.Max() : 0;

	public Account AddAccount(ZcashAccount account)
	{
		Account accountModel = new(account, this);
		this.AddAccount(accountModel);
		return accountModel;
	}

	public void AddAccount(Account account)
	{
		Requires.Argument(account.MemberOf == this, nameof(account), "This account does not belong to this HD wallet.");
		Requires.Argument(this.Zip32.Equals(account.ZcashAccount.HDDerivation?.Wallet), nameof(account), "This account does not belong to this HD wallet.");
		if (this.Accounts.ContainsKey(account.ZcashAccount.HDDerivation.Value.AccountIndex))
		{
			throw new ArgumentException("An account with this index already exists.", nameof(account));
		}

		this.accounts.Add(account.ZcashAccount.HDDerivation.Value.AccountIndex, account);
	}

	public Account AddAccount(uint index)
	{
		if (this.Accounts.ContainsKey(index))
		{
			throw new ArgumentException("An account with this index already exists.", nameof(index));
		}

		Account accountModel = new(new ZcashAccount(this.Zip32, index), this);
		this.accounts.Add(index, accountModel);
		return accountModel;
	}

	public bool RemoveAccount(uint index) => this.accounts.Remove(index);

	void IPersistableDataHelper.OnPropertyChanged(string propertyName) => this.OnPropertyChanged(propertyName);

	void IPersistableDataHelper.ClearDirtyFlagOnMembers()
	{
		this.Accounts.Values.ClearDirtyFlag();
	}

	protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) => this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

	protected void RaiseAndSetIfChanged<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
	{
		if (!EqualityComparer<T>.Default.Equals(field, value))
		{
			field = value;
			this.OnPropertyChanged(propertyName);
		}
	}

	private class Formatter : IMessagePackFormatter<HDWallet>
	{
		public HDWallet Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
		{
			options.Security.DepthStep(ref reader);

			HDWallet? wallet = null;

			int length = reader.ReadArrayHeader();
			if (length < 1)
			{
				throw new MessagePackSerializationException("Invalid HD wallet data.");
			}

			for (int i = 0; i < length; i++)
			{
				switch (i)
				{
					case 0:
						Zip32HDWallet zip32 = options.Resolver.GetFormatterWithVerify<Zip32HDWallet>().Deserialize(ref reader, options);
						wallet = new HDWallet(zip32);
						break;
					case 1:
						wallet!.Name = reader.ReadString() ?? string.Empty;
						break;
					case 2:
						wallet!.BirthdayHeight = reader.ReadUInt64();
						break;
					case 3:
						((AppSerializerOptions)options).HDWalletOwner = wallet;
						Account[] accounts = options.Resolver.GetFormatterWithVerify<Account[]>().Deserialize(ref reader, options);
						((AppSerializerOptions)options).HDWalletOwner = null;

						foreach (Account account in accounts)
						{
							wallet!.AddAccount(account);
						}

						break;
					case 4:
						wallet!.IsSeedPhraseBackedUp = reader.ReadBoolean();
						break;
					default:
						reader.Skip();
						break;
				}
			}

			reader.Depth--;

			return wallet!;
		}

		public void Serialize(ref MessagePackWriter writer, HDWallet value, MessagePackSerializerOptions options)
		{
			writer.WriteArrayHeader(5);

			options.Resolver.GetFormatterWithVerify<Zip32HDWallet>().Serialize(ref writer, value.Zip32, options);
			writer.Write(value.Name);
			writer.Write(value.BirthdayHeight);

			((AppSerializerOptions)options).HDWalletOwner = value;
			writer.WriteArrayHeader(value.Accounts.Count);
			IMessagePackFormatter<Account> accountFormatter = options.Resolver.GetFormatterWithVerify<Account>();
			foreach (Account account in value.Accounts.Values)
			{
				accountFormatter.Serialize(ref writer, account, options);
			}

			((AppSerializerOptions)options).HDWalletOwner = null;

			writer.Write(value.IsSeedPhraseBackedUp);
		}
	}
}
