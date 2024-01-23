// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using MessagePack;
using MessagePack.Formatters;

namespace Nerdbank.Zcash.App.Models;

/// <summary>
/// A wallet that contains all the accounts the user wants to track.
/// </summary>
[MessagePackFormatter(typeof(Formatter))]
public class ZcashWallet : INotifyPropertyChanged, IPersistableDataHelper
{
	private readonly ObservableCollection<HDWallet> hdWallets;
	private readonly ObservableCollection<Account> accounts;
	private bool isDirty = true;

	/// <summary>
	/// Initializes a new instance of the <see cref="ZcashWallet"/> class.
	/// </summary>
	public ZcashWallet()
		: this(Array.Empty<HDWallet>(), Array.Empty<Account>())
	{
	}

	private ZcashWallet(IReadOnlyList<HDWallet> hdWallets, IReadOnlyList<Account> accounts)
	{
		this.hdWallets = new(hdWallets);
		this.HDWallets = new(this.hdWallets);

		this.accounts = new(accounts);
		this.Accounts = new(this.accounts);

		this.accounts.CollectionChanged += (_, _) => this.OnPropertyChanged(nameof(this.IsEmpty));

		this.StartWatchingForDirtyChildren(this.hdWallets);
		this.StartWatchingForDirtyChildren(this.accounts);

		this.hdWallets.NotifyOnCollectionElementMemberChanged(nameof(HDWallet.IsBackedUp), (HDWallet? hd) => this.OnPropertyChanged(nameof(this.HDWalletsRequireBackup)));
	}

	/// <summary>
	/// Occurs when a property value changes.
	/// </summary>
	public event PropertyChangedEventHandler? PropertyChanged;

	public bool IsDirty
	{
		get => this.isDirty;
		set => this.SetIsDirty(ref this.isDirty, value);
	}

	public bool HDWalletsRequireBackup => this.hdWallets.Any(w => !w.IsBackedUp);

	/// <summary>
	/// Gets a value indicating whether the wallet has no lone accounts and no HD wallets (whether or not they are empty).
	/// </summary>
	public bool IsEmpty => this.Accounts.Count == 0;

	/// <summary>
	/// Gets a collection of all the seed phrase based wallets.
	/// </summary>
	public ReadOnlyObservableCollection<HDWallet> HDWallets { get; }

	/// <summary>
	/// Gets the collection of all accounts in the wallet.
	/// </summary>
	public ReadOnlyObservableCollection<Account> Accounts { get; }

	public bool TryGetHDWallet(Account account, [NotNullWhen(true)] out HDWallet? wallet) => this.TryGetHDWallet(account.ZcashAccount, out wallet);

	public bool TryGetHDWallet(ZcashAccount account, [NotNullWhen(true)] out HDWallet? wallet)
	{
		if (account.HDDerivation?.Wallet is Zip32HDWallet zip32)
		{
			wallet = this.HDWallets.FirstOrDefault(w => w.GetZip32HDWalletByNetwork(account.Network).Equals(zip32));
			return wallet is not null;
		}

		wallet = null;
		return false;
	}

	public IEnumerable<Account> GetAccountsUnder(HDWallet hd, ZcashNetwork network) => this.Accounts.Where(a => a.ZcashAccount.HDDerivation?.Wallet.Equals(hd.GetZip32HDWalletByNetwork(network)) is true);

	public uint? GetMaxAccountIndex(HDWallet hd, ZcashNetwork network) => this.GetAccountsUnder(hd, network).Max(a => a.ZcashAccount.HDDerivation?.AccountIndex);

	public bool TryGetAccountThatReceives(ZcashAddress receivingAddress, [NotNullWhen(true)] out Account? account)
	{
		account = this.Accounts.FirstOrDefault(a => a.ZcashAccount.AddressSendsToThisAccount(receivingAddress));
		return account is not null;
	}

	public IEnumerable<Account> GetAccountsContainingTransaction(TxId transactionId)
	{
		return this.Accounts.Where(a => a.Transactions.Any(tx => tx.TransactionId == transactionId));
	}

	public void Add(HDWallet hd)
	{
		this.hdWallets.Add(hd);
		this.StartWatchingForDirtyChild(hd);
	}

	/// <summary>
	/// Adds an account to the wallet.
	/// </summary>
	/// <param name="account">The account to add.</param>
	/// <returns>The account model wrapper that is created to wrap the <see cref="ZcashAccount"/>.</returns>
	public Account Add(ZcashAccount account)
	{
		Account accountModel = new(account);
		this.Add(accountModel);
		return accountModel;
	}

	public void Add(Account account)
	{
		Requires.Argument(account.ZcashAccount.BirthdayHeight is not null, nameof(account), "Set birthday height first.");

		if (account.ZcashAccount.HDDerivation is { Wallet.Mnemonic: { } mnemonic } && !this.TryGetHDWallet(account, out _))
		{
			HDWallet? hd = new HDWallet(mnemonic)
			{
				Name = $"{account.Name} HD",
			};
			this.Add(hd);
			this.StartWatchingForDirtyChild(hd);
		}

		this.accounts.Add(account);
		this.StartWatchingForDirtyChild(account);
	}

	public bool Remove(Account account, IContactManager? contactManager)
	{
		if (this.accounts.Remove(account))
		{
			if (contactManager is not null)
			{
				this.ScrubAccountReferenceFromContacts(account, contactManager);
			}

			return true;
		}

		return false;
	}

	void IPersistableDataHelper.OnPropertyChanged(string propertyName) => this.OnPropertyChanged(propertyName);

	void IPersistableDataHelper.ClearDirtyFlagOnMembers()
	{
		this.HDWallets.ClearDirtyFlag();
		this.Accounts.ClearDirtyFlag();
	}

	/// <summary>
	/// Raises the <see cref="PropertyChanged"/> event.
	/// </summary>
	/// <param name="propertyName">The name of the property that was changed.</param>
	protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) => this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

	private void ScrubAccountReferenceFromContacts(Account account, IContactManager contactManager)
	{
		// Enumerate contacts and remove any record of them observing a receiving address from the removed account.
		if (contactManager is not null)
		{
			foreach (Contact contact in contactManager.Contacts)
			{
				contact.RemoveSendingAddressAssignment(account);
			}
		}
	}

	private class Formatter : IMessagePackFormatter<ZcashWallet>
	{
		public ZcashWallet Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
		{
			HDWallet[] hdWallets = Array.Empty<HDWallet>();
			Account[] accounts = Array.Empty<Account>();

			int length = reader.ReadArrayHeader();
			for (int i = 0; i < length; i++)
			{
				switch (i)
				{
					case 0:
						hdWallets = options.Resolver.GetFormatterWithVerify<HDWallet[]>().Deserialize(ref reader, options);
						break;
					case 1:
						accounts = options.Resolver.GetFormatterWithVerify<Account[]>().Deserialize(ref reader, options);
						break;
					default:
						reader.Skip();
						break;
				}
			}

			ZcashWallet result = new(hdWallets, accounts);
			result.isDirty = false;
			return result;
		}

		public void Serialize(ref MessagePackWriter writer, ZcashWallet value, MessagePackSerializerOptions options)
		{
			writer.WriteArrayHeader(2);
			options.Resolver.GetFormatterWithVerify<IReadOnlyList<HDWallet>>().Serialize(ref writer, value.HDWallets, options);
			options.Resolver.GetFormatterWithVerify<IReadOnlyList<Account>>().Serialize(ref writer, value.Accounts, options);
		}
	}
}
